/// RASP / platform-verification configuration (docs/ENVIRONMENT_VARIABLES.md
/// §2). Request signing deliberately has NO shared secret here — it uses
/// per-device ECDSA key pairs (docs/SECURITY.md §1); never reintroduce a
/// build-time HMAC secret.
abstract final class SecurityConstants {
  static const String raspAndroidPackageName =
      String.fromEnvironment('RASP_ANDROID_PACKAGE_NAME');

  /// Comma-separated SHA-256 signing-certificate fingerprints (Base64).
  static const String raspAndroidSigningHash =
      String.fromEnvironment('RASP_ANDROID_SIGNING_HASH');

  static const String raspIosBundleId =
      String.fromEnvironment('RASP_IOS_BUNDLE_ID');

  static const String raspIosTeamId =
      String.fromEnvironment('RASP_IOS_TEAM_ID');

  /// Email that receives Talsec security reports.
  static const String raspWatcherMail =
      String.fromEnvironment('RASP_WATCHER_MAIL');

  static List<String> get raspAndroidSigningHashes =>
      raspAndroidSigningHash.isEmpty
          ? const []
          : raspAndroidSigningHash.split(',');
}
