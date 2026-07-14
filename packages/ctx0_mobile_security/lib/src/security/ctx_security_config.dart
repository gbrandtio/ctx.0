/// Wire-protocol version shared with the API's Ctx0.Security packages:
/// the two sides are compatible iff major.minor match. The API advertises
/// its version in the [ctxProtocolHeader] response header; a mismatch is
/// asserted in debug builds. Bump together with the server packages
/// whenever the signing string, ALE scheme, or security headers change.
const String ctxProtocolVersion = '1.1';
const String ctxProtocolHeader = 'X-Ctx-Protocol';

/// Configuration surface of the security plane (docs/SECURITY.md §4.1).
///
/// The security plane (RASP, device identity, interceptor chain, security
/// metadata) never reads app constants directly; everything app-specific
/// arrives through this object, built by `lib/app/security_bootstrap.dart`.
/// This is the seam that lets the plane ship as a compiled package while
/// the app keeps ownership of endpoints, headers, and RASP identity.
class CtxSecurityConfig {
  const CtxSecurityConfig({
    required this.resolveUri,
    required this.refreshTokenPath,
    required this.authEndpointPaths,
    required this.securityMetadataPath,
    required this.appInstancesPath,
    this.deviceIdHeader = defaultDeviceIdHeader,
    this.signatureHeader = defaultSignatureHeader,
    this.rasp = const CtxRaspConfig(),
  });

  static const String defaultDeviceIdHeader = 'X-App-Device-Id';
  static const String defaultSignatureHeader = 'X-App-Signature';

  /// Builds the absolute request [Uri] for an API-relative path
  /// (e.g. `/users/refresh`). Owned by the app so base URL and API
  /// versioning stay out of the security plane.
  final Uri Function(String path) resolveUri;

  /// Path of the token-refresh endpoint (rotating refresh tokens,
  /// docs/SECURITY.md §2).
  final String refreshTokenPath;

  /// Endpoint paths the AuthRefreshClient must never try to refresh on
  /// (login, refresh, logout, federated sign-in): a 401 there is a real
  /// authentication failure, not an expired session.
  final List<String> authEndpointPaths;

  /// Path of the unauthenticated ALE bootstrap endpoint
  /// (GET security metadata, docs/SECURITY.md §4.1).
  final String securityMetadataPath;

  /// Path of the device-registration endpoint used by self-healing
  /// registration (docs/SECURITY.md §1).
  final String appInstancesPath;

  /// Header carrying the per-device identifier.
  final String deviceIdHeader;

  /// Header carrying `timestamp:signature` (docs/SECURITY.md §4.2).
  /// Renamed per product (`X-<Name>-Signature`) — must match the API's
  /// RequestSigningMiddleware configuration.
  final String signatureHeader;

  final CtxRaspConfig rasp;
}

/// RASP / platform-verification identity (docs/ENVIRONMENT_VARIABLES.md §2).
/// Request signing deliberately has NO shared secret here — it uses
/// per-device ECDSA key pairs (docs/SECURITY.md §1); never reintroduce a
/// build-time HMAC secret.
class CtxRaspConfig {
  const CtxRaspConfig({
    this.watcherMail = '',
    this.androidPackageName = '',
    this.androidSigningHashes = const [],
    this.iosBundleId = '',
    this.iosTeamId = '',
  });

  /// Email that receives Talsec security reports.
  final String watcherMail;

  final String androidPackageName;

  /// SHA-256 signing-certificate fingerprints (Base64).
  final List<String> androidSigningHashes;

  final String iosBundleId;

  final String iosTeamId;

  bool get isConfigured =>
      watcherMail.isNotEmpty &&
      (androidSigningHashes.isNotEmpty || iosBundleId.isNotEmpty);
}
