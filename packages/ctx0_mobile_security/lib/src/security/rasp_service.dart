import 'dart:io';

import 'package:flutter/foundation.dart';
import 'package:freerasp/freerasp.dart';

import 'ctx_security_config.dart';

/// Runtime Application Self Protection via Talsec/freerasp
/// (docs/SECURITY.md §3): detects root/jailbreak, emulators, debuggers,
/// tampering, hooking, and unofficial installs. Reaction policy is
/// **Force Close** — the process ends before data can be exfiltrated.
///
/// Active only in release builds with RASP_* configured
/// (docs/ENVIRONMENT_VARIABLES.md): debug/profile runs would otherwise be
/// killed by their own debugger and emulator.
class RaspService {
  RaspService(this._config);

  final CtxRaspConfig _config;

  Future<void> init() async {
    if (!kReleaseMode || !_config.isConfigured) {
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
      watcherMail: _config.watcherMail,
      androidConfig: _config.androidSigningHashes.isEmpty
          ? null
          : AndroidConfig(
              packageName: _config.androidPackageName,
              signingCertHashes: _config.androidSigningHashes,
            ),
      iosConfig: _config.iosBundleId.isEmpty
          ? null
          : IOSConfig(
              bundleIds: [_config.iosBundleId],
              teamId: _config.iosTeamId,
            ),
    ));
  }

  Never _forceClose() => exit(1);
}
