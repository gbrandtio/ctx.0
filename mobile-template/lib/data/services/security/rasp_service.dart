import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:freerasp/freerasp.dart';

import '../../../core/constants/security_constants.dart';

/// Runtime Application Self Protection via Talsec/freerasp
/// (docs/SECURITY.md §3): detects root/jailbreak, emulators, debuggers,
/// tampering, hooking, and unofficial installs. Reaction policy is
/// **Force Close** — the process ends before data can be exfiltrated.
///
/// Active only in release builds with RASP_* configured
/// (docs/ENVIRONMENT_VARIABLES.md): debug/profile runs would otherwise be
/// killed by their own debugger and emulator.
class RaspService {
  bool get _configured =>
      SecurityConstants.raspWatcherMail.isNotEmpty &&
      (SecurityConstants.raspAndroidSigningHashes.isNotEmpty ||
          SecurityConstants.raspIosBundleId.isNotEmpty);

  Future<void> init() async {
    if (!kReleaseMode || !_configured) {
      debugPrint('RASP inactive (debug build or RASP_* not configured)');
      return;
    }

    Talsec.instance.attachListener(ThreatCallback(
      onPrivilegedAccess: _forceClose, // root / jailbreak
      onDebug: _forceClose,
      onSimulator: _forceClose,
      onAppIntegrity: _forceClose, // tampering / re-signing
      onHooks: _forceClose, // Frida & friends
      onUnofficialStore: _forceClose,
      onObfuscationIssues: _forceClose,
    ));

    await Talsec.instance.start(TalsecConfig(
      watcherMail: SecurityConstants.raspWatcherMail,
      androidConfig: SecurityConstants.raspAndroidSigningHashes.isEmpty
          ? null
          : AndroidConfig(
              packageName: SecurityConstants.raspAndroidPackageName,
              signingCertHashes: SecurityConstants.raspAndroidSigningHashes,
            ),
      iosConfig: SecurityConstants.raspIosBundleId.isEmpty
          ? null
          : IOSConfig(
              bundleIds: [SecurityConstants.raspIosBundleId],
              teamId: SecurityConstants.raspIosTeamId,
            ),
    ));
  }

  Never _forceClose() => exit(1);
}
