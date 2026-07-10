import 'dart:async';

import 'package:firebase_core/firebase_core.dart';
import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import 'app/app.dart';
import 'app/modules.dart';
import 'core/utils/app_bloc_observer.dart';
import 'data/repositories/auth_repository.dart';
import 'data/services/api/api_service_factory.dart';
import 'data/services/api/user_api_service.dart';
import 'data/services/security/device_identity_service.dart';
import 'data/services/storage/hive_cache_service.dart';
import 'data/services/storage/prefs_service.dart';
import 'data/services/storage/secure_storage_service.dart';

/// Bootstrap (docs/FLUTTER_ARCHITECTURE.md §5, docs/SECURITY.md §4.1):
/// storage → device identity → interceptor chain → repositories →
/// runApp(App). Session restore runs behind the splash screen.
Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  Bloc.observer = const AppBlocObserver();

  // Push notifications need the platform Firebase config
  // (google-services.json / GoogleService-Info.plist). Until it is added,
  // the app runs without push (PushTokenService degrades gracefully).
  try {
    await Firebase.initializeApp();
  } on Exception catch (e) {
    debugPrint('Firebase not configured — push disabled: $e');
  }

  final prefs = await PrefsService.create();
  final secureStorage = SecureStorageService();
  final cacheService = HiveCacheService();
  await cacheService.init();

  final deviceIdentity = DeviceIdentityService(secureStorage);
  await deviceIdentity.init();

  late final AuthRepository authRepository;
  final apiFactory = ApiServiceFactory(
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
    apiClient: apiFactory.client,
  ));
}
