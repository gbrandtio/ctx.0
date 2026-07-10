import 'dart:async';

import '../../core/result/result.dart';
import '../../models/auth_session.dart';
import '../../models/user.dart';
import '../services/api/interceptors/caching_client.dart';
import '../services/api/user_api_service.dart';
import '../services/storage/prefs_service.dart';
import '../services/storage/secure_storage_service.dart';

/// Authentication state — the single source of truth every Bloc derives
/// from (docs/FLUTTER_ARCHITECTURE.md §1; docs/STATE_MANAGEMENT.md §6).
sealed class AuthState {
  const AuthState();
}

/// Session restore has not completed yet (splash/redirect hold).
final class AuthUnknown extends AuthState {
  const AuthUnknown();
}

final class Authenticated extends AuthState {
  const Authenticated(this.user);
  final User user;
}

final class Unauthenticated extends AuthState {
  const Unauthenticated();
}

/// The auth SSOT (docs/APP_SHELL.md §1): owns the session, exposes
/// [authStateChanges] which drives the router redirect and lets global
/// cubits reset themselves on logout. Blocs never talk to each other —
/// they all observe this stream.
class AuthRepository {
  AuthRepository({
    required UserApiService userApi,
    required SecureStorageService secureStorage,
    required PrefsService prefs,
    required CachingClient cachingClient,
  })  : _userApi = userApi,
        _secureStorage = secureStorage,
        _prefs = prefs,
        _cachingClient = cachingClient;

  final UserApiService _userApi;
  final SecureStorageService _secureStorage;
  final PrefsService _prefs;
  final CachingClient _cachingClient;

  final _controller = StreamController<AuthState>.broadcast();
  AuthState _current = const AuthUnknown();

  AuthState get currentState => _current;

  /// Emits the current state immediately to every new listener, then all
  /// subsequent changes.
  Stream<AuthState> get authStateChanges async* {
    yield _current;
    yield* _controller.stream;
  }

  void _emit(AuthState state) {
    // Compare-before-write: identical re-emissions cause reactive loops
    // (docs/FLUTTER_ARCHITECTURE.md §6C).
    if (state.runtimeType == _current.runtimeType &&
        state is! Authenticated) {
      return;
    }
    if (state is Authenticated &&
        _current is Authenticated &&
        state.user == (_current as Authenticated).user) {
      return;
    }
    _current = state;
    _controller.add(state);
  }

  /// Restores the session at bootstrap: with a stored token, fetch the
  /// profile; otherwise settle on unauthenticated.
  Future<void> restoreSession() async {
    final token = await _secureStorage.readAccessToken();
    if (token == null) {
      _emit(const Unauthenticated());
      return;
    }
    try {
      final user = await _userApi.getMe(forceRefresh: true);
      _emit(Authenticated(user));
    } on Exception {
      // Expired/invalid session or offline: the AuthRefreshClient will
      // have purged tokens if the refresh was rejected.
      _emit(const Unauthenticated());
    }
  }

  Future<Result<User>> login(String email, String password) =>
      _establishSession(() => _userApi.login(email, password));

  Future<Result<User>> signup({
    required String email,
    required String password,
    String? displayName,
    required Map<String, bool> consents,
  }) =>
      _establishSession(() => _userApi.signup(
            email: email,
            password: password,
            displayName: displayName,
            consents: consents,
          ));

  Future<Result<User>> signInWithGoogle(String idToken) =>
      _establishSession(() => _userApi.googleSignIn(idToken));

  Future<Result<void>> verifyEmail(String code) async {
    try {
      await _userApi.verifyEmail(code);
      final current = _current;
      if (current is Authenticated) {
        _emit(Authenticated(current.user.copyWith(emailVerified: true)));
      }
      return const Result.success(null);
    } on Exception catch (e) {
      return Result.failure(e);
    }
  }

  Future<Result<void>> resendVerification() async {
    try {
      await _userApi.resendVerification();
      return const Result.success(null);
    } on Exception catch (e) {
      return Result.failure(e);
    }
  }

  /// Refreshes the current user (e.g. after profile edit); merges via
  /// copyWith so bare models never clobber richer local state
  /// (docs/FLUTTER_ARCHITECTURE.md §6D).
  Future<Result<User>> refreshUser({bool forceRefresh = false}) async {
    try {
      final fetched = await _userApi.getMe(forceRefresh: forceRefresh);
      final current = _current;
      final merged = current is Authenticated
          ? current.user.copyWith(
              displayName: fetched.displayName,
              emailVerified: fetched.emailVerified,
            )
          : fetched;
      _emit(Authenticated(merged));
      return Result.success(merged);
    } on Exception catch (e) {
      return Result.failure(e);
    }
  }

  Future<Result<User>> updateProfile({String? displayName}) async {
    try {
      final user = await _userApi.updateMe(displayName: displayName);
      _emit(Authenticated(user));
      await _cachingClient.invalidatePattern('/users/');
      return Result.success(user);
    } on Exception catch (e) {
      return Result.failure(e);
    }
  }

  Future<Result<void>> logout() async {
    final refreshToken = await _secureStorage.readRefreshToken();
    try {
      if (refreshToken != null) await _userApi.logout(refreshToken);
    } on Exception {
      // Best-effort server-side revocation; local purge happens regardless.
    }
    await _purgeLocalState();
    _emit(const Unauthenticated());
    return const Result.success(null);
  }

  /// GDPR delete account (docs/APP_SHELL.md §4): anonymizing server
  /// delete, then full local purge, then logout.
  Future<Result<void>> deleteAccount() async {
    try {
      await _userApi.deleteMe();
    } on Exception catch (e) {
      return Result.failure(e);
    }
    await _secureStorage.clearAll();
    await _purgeLocalState();
    _emit(const Unauthenticated());
    return const Result.success(null);
  }

  /// GDPR data export request; delivery is notified via push.
  Future<Result<void>> requestDataExport() async {
    try {
      await _userApi.requestDataExport();
      return const Result.success(null);
    } on Exception catch (e) {
      return Result.failure(e);
    }
  }

  /// Called by the AuthRefreshClient when a token refresh is rejected.
  void onSessionExpired() => _emit(const Unauthenticated());

  Future<void> _purgeLocalState() async {
    await _secureStorage.deleteTokens();
    await _cachingClient.clearCache();
    // Prevent preference leakage to the next user (docs/SECURITY.md §5).
    await _prefs.clear();
  }

  Future<Result<User>> _establishSession(
    Future<AuthSession> Function() call,
  ) async {
    try {
      final session = await call();
      await _secureStorage.writeTokens(
        accessToken: session.accessToken,
        refreshToken: session.refreshToken,
      );
      _emit(Authenticated(session.user));
      return Result.success(session.user);
    } on Exception catch (e) {
      return Result.failure(e);
    }
  }

  void dispose() => _controller.close();
}
