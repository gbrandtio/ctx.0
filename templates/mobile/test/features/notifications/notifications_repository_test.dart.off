import 'dart:async';

import 'package:app_template/data/repositories/auth_repository.dart';
import 'package:app_template/features/notifications/data/notification_api_service.dart';
import 'package:app_template/features/notifications/data/notifications_repository.dart';
import 'package:app_template/features/notifications/data/push_token_service.dart';
import 'package:app_template/models/user.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

class _MockApi extends Mock implements NotificationApiService {}

class _MockPushTokens extends Mock implements PushTokenService {}

class _MockAuthRepository extends Mock implements AuthRepository {}

void main() {
  late _MockApi api;
  late _MockPushTokens pushTokens;
  late _MockAuthRepository authRepository;
  late StreamController<AuthState> authStates;
  late List<Future<void> Function()> logoutHooks;

  setUp(() {
    api = _MockApi();
    pushTokens = _MockPushTokens();
    authRepository = _MockAuthRepository();
    authStates = StreamController<AuthState>.broadcast();
    logoutHooks = [];
    when(() => authRepository.authStateChanges)
        .thenAnswer((_) => authStates.stream);
    when(() => authRepository.registerLogoutHook(any())).thenAnswer(
      (invocation) => logoutHooks
          .add(invocation.positionalArguments.first as Future<void> Function()),
    );
    when(() => pushTokens.onTokenRefresh)
        .thenAnswer((_) => const Stream.empty());
    when(() => pushTokens.deleteToken()).thenAnswer((_) async {});
    when(() => api.registerFirebaseToken(any())).thenAnswer((_) async {});
    when(() => api.unregisterFirebaseToken()).thenAnswer((_) async {});
  });

  tearDown(() => authStates.close());

  NotificationsRepository build() => NotificationsRepository(
        api: api,
        pushTokens: pushTokens,
        authRepository: authRepository,
      );

  test('registers the FCM token when a session is established', () async {
    when(() => pushTokens.requestToken()).thenAnswer((_) async => 'fcm-1');
    build();

    authStates.add(const Authenticated(User(id: 'u1', email: 'a@b.com')));
    await Future<void>.delayed(Duration.zero);

    verify(() => api.registerFirebaseToken('fcm-1')).called(1);
  });

  test('login still succeeds when push is unavailable (token null)',
      () async {
    when(() => pushTokens.requestToken()).thenAnswer((_) async => null);
    build();

    authStates.add(const Authenticated(User(id: 'u1', email: 'a@b.com')));
    await Future<void>.delayed(Duration.zero);

    verifyNever(() => api.registerFirebaseToken(any()));
  });

  test('logout hook unregisters server-side and deletes the local token',
      () async {
    when(() => pushTokens.requestToken()).thenAnswer((_) async => 'fcm-1');
    build();
    authStates.add(const Authenticated(User(id: 'u1', email: 'a@b.com')));
    await Future<void>.delayed(Duration.zero);

    expect(logoutHooks, hasLength(1));
    await logoutHooks.single();

    verify(() => api.unregisterFirebaseToken()).called(1);
    verify(() => pushTokens.deleteToken()).called(1);
  });

  test('logout hook is a no-op when nothing was registered', () async {
    when(() => pushTokens.requestToken()).thenAnswer((_) async => null);
    build();

    for (final hook in logoutHooks) {
      await hook();
    }

    verifyNever(() => api.unregisterFirebaseToken());
  });
}
