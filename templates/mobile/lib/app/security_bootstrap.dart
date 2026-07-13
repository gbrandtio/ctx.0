import '../core/constants/api_constants.dart';
import '../core/constants/security_constants.dart';
import 'package:ctx0_mobile_security/ctx0_mobile_security.dart';

/// The ONLY bridge between app constants and the security plane
/// (docs/SECURITY.md §4.1): endpoints, headers, and RASP identity flow
/// into [CtxSecurityConfig] here; the plane itself never imports
/// `core/constants/`. Keep every app-specific security value in this file.
CtxSecurityConfig buildSecurityConfig() => CtxSecurityConfig(
  resolveUri: ApiConstants.uri,
  refreshTokenPath: ApiConstants.refreshToken,
  authEndpointPaths: const [
    ApiConstants.login,
    ApiConstants.refreshToken,
    ApiConstants.logout,
    ApiConstants.googleSignIn,
  ],
  securityMetadataPath: ApiConstants.securityMetadata,
  appInstancesPath: ApiConstants.appInstances,
  // Must match the API's AleOptions (DeviceIdHeader/SignatureHeader);
  // renamed per product by `ctx0 create` (X-<Name>-...).
  deviceIdHeader: 'X-App-Device-Id',
  signatureHeader: 'X-App-Signature',
  rasp: CtxRaspConfig(
    watcherMail: SecurityConstants.raspWatcherMail,
    androidPackageName: SecurityConstants.raspAndroidPackageName,
    androidSigningHashes: SecurityConstants.raspAndroidSigningHashes,
    iosBundleId: SecurityConstants.raspIosBundleId,
    iosTeamId: SecurityConstants.raspIosTeamId,
  ),
);
