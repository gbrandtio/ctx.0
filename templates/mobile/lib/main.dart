import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:http/http.dart' as http;

import 'app/app.dart';
import 'app/modules.dart';
import 'app/security_bootstrap.dart';
import 'core/utils/app_bloc_observer.dart';
import 'core/utils/logging_service.dart';
import 'core/utils/time_provider.dart';
import 'data/repositories/auth_repository.dart';
import 'package:ctx0_mobile_security/ctx0_mobile_security.dart';
import 'data/services/api/user_api_service.dart';
import 'data/services/storage/prefs_service.dart';
// ctx:app_updates:begin
import 'package:package_info_plus/package_info_plus.dart';
import 'features/app_updates/app_updates_module.dart';
import 'features/app_updates/data/version_check_client.dart';
// ctx:app_updates:end

/// Bootstrap (docs/FLUTTER_ARCHITECTURE.md §5, docs/SECURITY.md §4.1):
/// storage → device identity → interceptor chain → repositories →
/// runApp(App). Session restore runs behind the splash screen.
Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  const loggingService = ConsoleLoggingService();
  const timeProvider = SystemTimeProvider();

  Bloc.observer = const AppBlocObserver(loggingService);

  // RASP first (docs/SECURITY.md §4.1): a compromised environment must be
  // detected before any secret leaves secure storage.
  final securityConfig = buildSecurityConfig();
  await RaspService(securityConfig.rasp).init();

  final prefs = await PrefsService.create();
  final secureStorage = SecureStorageService();
  final cacheService = HiveCacheService();
  await cacheService.init();

  final deviceIdentity = DeviceIdentityService(secureStorage);
  await deviceIdentity.init();

  // Vendor SDK bootstrap is owned by the module that needs it
  // (FeatureModule.init) — main() stays vendor-free so enabling or
  // disabling an integration never touches this file
  // (docs/INTEGRATIONS.md).
  for (final module in appModules) {
    await module.init();
  }

  late final AuthRepository authRepository;
  final apiFactory = ApiServiceFactory(
    config: securityConfig,
    secureStorage: secureStorage,
    deviceIdentity: deviceIdentity,
    cacheService: cacheService,
    onSessionExpired: () => authRepository.onSessionExpired(),
  );

  // Every outbound request must carry the client-version header and observe
  // a 426, including auth traffic (login/signup/refresh/logout) — otherwise
  // an armed server-side version gate rejects auth with no upgrade prompt.
  // The wrapper is applied to BOTH the auth client and the module client so
  // no request bypasses the gate (docs/INTEGRATIONS.md app_updates).
  http.Client authClient = apiFactory.client;
  http.Client apiClient = apiFactory.cachingClient;
  // ctx:app_updates:begin
  final packageInfo = await PackageInfo.fromPlatform();
  http.Client wrapVersion(http.Client inner) => VersionCheckClient(
        inner: inner,
        clientVersion: packageInfo.version,
        onUpgradeRequired: () => updateRequiredNotifier.value = true,
      );
  authClient = wrapVersion(authClient);
  apiClient = wrapVersion(apiClient);
  // ctx:app_updates:end

  authRepository = AuthRepository(
    userApi: UserApiService(authClient),
    secureStorage: secureStorage,
    prefs: prefs,
    cachingClient: apiFactory.cachingClient,
  );

  // Don't block the first frame: the router holds on /splash until the
  // restore settles (AuthUnknown → Authenticated/Unauthenticated).
  unawaited(authRepository.restoreSession());

  runApp(
    App(
      modules: appModules,
      authRepository: authRepository,
      prefs: prefs,
      apiClient: apiClient,
      cachingClient: apiFactory.cachingClient,
      timeProvider: timeProvider,
      loggingService: loggingService,
    ),
  );
}
