import 'dart:io';

/// Raised when the runtime environment fails a security check.
class CtxSecurityException implements Exception {
  const CtxSecurityException(this.message);
  final String message;
  @override
  String toString() => 'CtxSecurityException: $message';
}

/// Runtime Application Self-Protection gate. Runs before the app boots and
/// refuses to start on a device showing root (Android) or jailbreak (iOS)
/// artifacts. Extend the checks for your threat model; do not remove them.
class RaspGate {
  const RaspGate._();

  static const List<String> _rootArtifacts = <String>[
    '/system/app/Superuser.apk',
    '/system/xbin/su',
    '/system/bin/su',
    '/sbin/su',
    '/system/bin/magisk',
    '/data/adb/magisk',
  ];

  static const List<String> _jailbreakArtifacts = <String>[
    '/Applications/Cydia.app',
    '/Library/MobileSubstrate/MobileSubstrate.dylib',
    '/bin/bash',
    '/usr/sbin/sshd',
    '/private/var/lib/apt/',
  ];

  /// Enforce device integrity. Throws [CtxSecurityException] on a compromised device.
  static Future<void> enforce() async {
    final artifacts = Platform.isAndroid
        ? _rootArtifacts
        : Platform.isIOS
        ? _jailbreakArtifacts
        : const <String>[];

    for (final path in artifacts) {
      if (await _exists(path)) {
        throw CtxSecurityException('Compromised device: found $path');
      }
    }
  }

  static Future<bool> _exists(String path) async {
    try {
      return await File(path).exists() || await Directory(path).exists();
    } on FileSystemException {
      return false;
    }
  }
}
