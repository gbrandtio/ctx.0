import 'package:app_template/core/models/problem_details.dart';
import 'package:app_template/core/result/result.dart';
import 'package:app_template/core/utils/app_exception.dart';
import 'package:app_template/data/repositories/auth_repository.dart';
import 'package:ctx0_mobile_security/ctx0_mobile_security.dart';
import 'package:app_template/data/services/api/user_api_service.dart';
import 'package:app_template/data/services/storage/prefs_service.dart';
import 'package:app_template/models/auth_session.dart';
import 'package:app_template/models/user.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

class _MockUserApi extends Mock implements UserApiService {}

class _MockSecureStorage extends Mock implements SecureStorageService {}

class _MockPrefs extends Mock implements PrefsService {}

class _MockCachingClient extends Mock implements CachingClient {}

const _user = User(id: 'u1', email: 'a@b.com', displayName: 'Ada');
const _session = AuthSession(
  accessToken: 'access',
  refreshToken: 'refresh',
  user: _user,
);

void main() {
  late _MockUserApi userApi;
  late _MockSecureStorage secureStorage;
  late _MockPrefs prefs;
  late _MockCachingClient cachingClient;
  late AuthRepository repository;

  setUp(() {
    userApi = _MockUserApi();
    secureStorage = _MockSecureStorage();
    prefs = _MockPrefs();
    cachingClient = _MockCachingClient();
    when(
      () => secureStorage.writeTokens(
        accessToken: any(named: 'accessToken'),
        refreshToken: any(named: 'refreshToken'),
      ),
    ).thenAnswer((_) async {});
    when(() => secureStorage.writeUserId(any())).thenAnswer((_) async {});
    when(() => secureStorage.deleteTokens()).thenAnswer((_) async {});
    when(() => secureStorage.clearAll()).thenAnswer((_) async {});
    when(() => prefs.clear()).thenAnswer((_) async {});
    // No GDPR banner choice recorded by default → no consent replay on login.
    when(() => prefs.hasSeenGdprBanner).thenReturn(false);
    when(() => prefs.trackingConsentGranted).thenReturn(false);
    when(() => cachingClient.clearCache()).thenAnswer((_) async {});
    when(() => cachingClient.invalidatePattern(any())).thenAnswer((_) async {});
    repository = AuthRepository(
      userApi: userApi,
      secureStorage: secureStorage,
      prefs: prefs,
      cachingClient: cachingClient,
    );
  });

  test('starts unknown so the router can hold on splash', () {
    expect(repository.currentState, isA<AuthUnknown>());
  });

  test('login success stores both tokens and emits Authenticated', () async {
    when(
      () => userApi.login('a@b.com', 'pw'),
    ).thenAnswer((_) async => _session);

    final result = await repository.login('a@b.com', 'pw');

    expect(result, isA<Success<AuthSession>>());
    expect(repository.currentState, isA<Authenticated>());
    verify(
      () => secureStorage.writeTokens(
        accessToken: 'access',
        refreshToken: 'refresh',
      ),
    ).called(1);
  });

  test(
    'replays a pending GDPR consent choice to the API on login (M3)',
    () async {
      when(() => userApi.login(any(), any())).thenAnswer((_) async => _session);
      // The user accepted tracking in the pre-login banner; the server row
      // still has no consent, so login must push the local choice.
      when(() => prefs.hasSeenGdprBanner).thenReturn(true);
      when(() => prefs.trackingConsentGranted).thenReturn(true);
      when(
        () => userApi.updateUser(any(), hasTrackingConsent: any(named: 'hasTrackingConsent')),
      ).thenAnswer((_) async => _user);

      await repository.login('a@b.com', 'pw');

      verify(() => userApi.updateUser('u1', hasTrackingConsent: true)).called(1);
    },
  );

  test(
    'login failure returns Failure and stays unauthenticated-safe',
    () async {
      when(
        () => userApi.login(any(), any()),
      ).thenThrow(const AppException(ProblemDetails(status: 401)));

      final result = await repository.login('a@b.com', 'wrong');

      expect(result, isA<Failure<AuthSession>>());
      expect(repository.currentState, isNot(isA<Authenticated>()));
      verifyNever(
        () => secureStorage.writeTokens(
          accessToken: any(named: 'accessToken'),
          refreshToken: any(named: 'refreshToken'),
        ),
      );
    },
  );

  test(
    'restoreSession without a stored token settles unauthenticated',
    () async {
      when(() => secureStorage.readAccessToken()).thenAnswer((_) async => null);
      when(() => secureStorage.readUserId()).thenAnswer((_) async => null);

      await repository.restoreSession();

      expect(repository.currentState, isA<Unauthenticated>());
      verifyNever(
        () => userApi.getUser(any(), forceRefresh: any(named: 'forceRefresh')),
      );
    },
  );

  test('logout revokes the refresh token and purges all local state', () async {
    when(() => userApi.login(any(), any())).thenAnswer((_) async => _session);
    when(
      () => secureStorage.readRefreshToken(),
    ).thenAnswer((_) async => 'refresh');
    when(() => userApi.logout('refresh')).thenAnswer((_) async {});
    await repository.login('a@b.com', 'pw');

    await repository.logout();

    expect(repository.currentState, isA<Unauthenticated>());
    verify(() => userApi.logout('refresh')).called(1);
    verify(() => secureStorage.deleteTokens()).called(1);
    verify(() => cachingClient.clearCache()).called(1);
    // No preference leakage to the next user (docs/SECURITY.md §5).
    verify(() => prefs.clear()).called(1);
  });

  test('deleteAccount purges secure storage entirely (GDPR)', () async {
    // A session must exist first — delete addresses /users/{id}.
    when(() => userApi.login(any(), any())).thenAnswer((_) async => _session);
    when(() => userApi.deleteUser('u1')).thenAnswer((_) async {});
    await repository.login('a@b.com', 'pw');

    final result = await repository.deleteAccount();

    expect(result, isA<Success<void>>());
    expect(repository.currentState, isA<Unauthenticated>());
    verify(() => userApi.deleteUser('u1')).called(1);
    verify(() => secureStorage.clearAll()).called(1);
    verify(() => cachingClient.clearCache()).called(1);
  });

  test('authStateChanges replays the current state to new listeners', () async {
    when(() => userApi.login(any(), any())).thenAnswer((_) async => _session);
    await repository.login('a@b.com', 'pw');

    final first = await repository.authStateChanges.first;

    expect(first, isA<Authenticated>());
  });
}
