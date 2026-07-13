/// The permanent mobile security plane (docs/SECURITY.md): RASP, device
/// identity, the request-security interceptor chain
/// (Caching → AuthRefresh → Signing → ALE → network), secure storage, and
/// the ALE metadata bootstrap.
///
/// Everything app-specific enters through [CtxSecurityConfig], built by
/// the app's `lib/app/security_bootstrap.dart`. The chain is assembled
/// only by [ApiServiceFactory]; its order is contractual and mirrors the
/// server pipeline.
library;

export 'src/api/api_service_factory.dart';
export 'src/api/interceptors/ale_client.dart';
export 'src/api/interceptors/auth_refresh_client.dart';
export 'src/api/interceptors/caching_client.dart';
export 'src/api/interceptors/http_interceptor_utils.dart';
export 'src/api/interceptors/secure_device_signing_client.dart';
export 'src/api/security_metadata_service.dart';
export 'src/models/cache_entry.dart';
export 'src/security/crypto_utils.dart';
export 'src/security/ctx_security_config.dart';
export 'src/security/device_identity_service.dart';
export 'src/security/rasp_service.dart';
export 'src/storage/hive_cache_service.dart';
export 'src/storage/secure_storage_service.dart';
