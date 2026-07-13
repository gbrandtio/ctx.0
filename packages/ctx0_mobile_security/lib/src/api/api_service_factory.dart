import 'package:http/http.dart' as http;

import '../security/ctx_security_config.dart';
import '../security/device_identity_service.dart';
import '../storage/hive_cache_service.dart';
import '../storage/secure_storage_service.dart';
import 'interceptors/ale_client.dart';
import 'interceptors/auth_refresh_client.dart';
import 'interceptors/caching_client.dart';
import 'interceptors/secure_device_signing_client.dart';
import 'security_metadata_service.dart';

/// Assembles the Chain of Responsibility the docs mandate
/// (docs/HTTP_HANDLING.md "Interceptor Orchestration"):
///
///   CachingClient → AuthRefreshClient → SecureDeviceSigningClient
///     → AleClient → network
///
/// Caching decides first (a hit never touches the network); the signature
/// is computed over the plaintext; ALE encrypts last, just before the
/// wire — mirroring the server pipeline, which decrypts and then verifies.
class ApiServiceFactory {
  ApiServiceFactory({
    required CtxSecurityConfig config,
    required SecureStorageService secureStorage,
    required DeviceIdentityService deviceIdentity,
    required HiveCacheService cacheService,
    void Function()? onSessionExpired,
    http.Client? networkClient,
  }) {
    final network = networkClient ?? http.Client();
    securityMetadata = SecurityMetadataService(network, config);
    final ale = AleClient(network, securityMetadata);
    final signing = SecureDeviceSigningClient(ale, deviceIdentity, config);
    final authRefresh = AuthRefreshClient(
      signing,
      secureStorage,
      config,
      onSessionExpired: onSessionExpired,
    );
    cachingClient = CachingClient(authRefresh, cacheService);
  }

  late final SecurityMetadataService securityMetadata;
  late final CachingClient cachingClient;

  /// The fully-interception client every ApiService uses.
  http.Client get client => cachingClient;
}
