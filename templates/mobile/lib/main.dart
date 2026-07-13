import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

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
  authRepository = AuthRepository(
    userApi: UserApiService(apiFactory.client),
    secureStorage: secureStorage,
    prefs: prefs,
    cachingClient: apiFactory.cachingClient,
  );

  // Don't block the first frame: the router holds on /splash until the
  // restore settles (AuthUnknown → Authenticated/Unauthenticated).
  unawaited(authRepository.restoreSession());

  runApp(App(
    modules: appModules,
    authRepository: authRepository,
    prefs: prefs,
    apiClient: apiFactory.cachingClient,
    timeProvider: timeProvider,
    loggingService: loggingService,
  ));
}
