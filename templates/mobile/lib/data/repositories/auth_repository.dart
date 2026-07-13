import 'dart:async';

import '../../core/result/result.dart';
import '../../models/auth_session.dart';
import '../../models/user.dart';
import 'package:ctx0_mobile_security/ctx0_mobile_security.dart';
import '../services/api/user_api_service.dart';
import '../services/storage/prefs_service.dart';

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
  }) : _userApi = userApi,
       _secureStorage = secureStorage,
       _prefs = prefs,
       _cachingClient = cachingClient;

  final UserApiService _userApi;
  final SecureStorageService _secureStorage;
  final PrefsService _prefs;
  final CachingClient _cachingClient;

  final _controller = StreamController<AuthState>.broadcast();
  final _logoutHooks = <Future<void> Function()>[];
  AuthState _current = const AuthUnknown();

  /// Registers work that must run while the session is still valid, just
  /// before logout purges it (e.g. the notifications module unregisters
  /// its FCM token). Hooks are best-effort: a failing hook never blocks
  /// logout.
  void registerLogoutHook(Future<void> Function() hook) =>
      _logoutHooks.add(hook);

  Future<void> _runLogoutHooks() async {
    for (final hook in _logoutHooks) {
      try {
        await hook();
      } on Exception {
        // Best-effort by contract.
      }
    }
  }

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
    if (state.runtimeType == _current.runtimeType && state is! Authenticated) {
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

  /// Restores the session at bootstrap: with a stored token and user id,
  /// re-fetch the profile; otherwise settle on unauthenticated.
  Future<void> restoreSession() async {
    final token = await _secureStorage.readAccessToken();
    final userId = await _secureStorage.readUserId();
    if (token == null || userId == null) {
      _emit(const Unauthenticated());
      return;
    }
    try {
      final user = await _userApi.getUser(userId, forceRefresh: true);
      _emit(Authenticated(user));
    } on Exception {
      // Expired/invalid session or offline: the AuthRefreshClient will
      // have purged tokens if the refresh was rejected.
      _emit(const Unauthenticated());
    }
  }

  /// Registration step 1 (AUTHENTICATION.md): request a verification code.
  /// The account is created later by [register] with that code.
  Future<Result<void>> sendSignupCode(String email) async {
    try {
      await _userApi.sendSignupCode(email);
      return const Result.success(null);
    } on Exception catch (e) {
      return Result.failure(e);
    }
  }

  Future<Result<AuthSession>> login(String usernameOrEmail, String password) =>
      _establishSession(() => _userApi.login(usernameOrEmail, password));

  /// Registration step 2: create the account with the code and start the
  /// session. [username] is derived by the caller from the email when the
  /// signup form does not collect one.
  Future<Result<AuthSession>> register({
    required String username,
    required String email,
    required String password,
    required String verificationCode,
    String? displayName,
    required Map<String, bool> consents,
  }) => _establishSession(
    () => _userApi.register(
      username: username,
      email: email,
      password: password,
      verificationCode: verificationCode,
      name: displayName,
      consents: consents,
    ),
  );

  Future<Result<AuthSession>> signInWithGoogle(String idToken) =>
      _establishSession(() => _userApi.googleSignIn(idToken));

  /// Refreshes the current user (e.g. after profile edit); merges via
  /// copyWith so bare models never clobber richer local state
  /// (docs/FLUTTER_ARCHITECTURE.md §6D).
  Future<Result<User>> refreshUser({bool forceRefresh = false}) async {
    final current = _current;
    if (current is! Authenticated) {
      return Result.failure(StateError('No active session.'));
    }
    try {
      final fetched = await _userApi.getUser(
        current.user.id,
        forceRefresh: forceRefresh,
      );
      final merged = current.user.copyWith(displayName: fetched.displayName);
      _emit(Authenticated(merged));
      return Result.success(merged);
    } on Exception catch (e) {
      return Result.failure(e);
    }
  }

  Future<Result<User>> updateProfile({
    String? displayName,
    bool? hasTrackingConsent,
  }) async {
    final current = _current;
    if (current is! Authenticated) {
      return Result.failure(StateError('No active session.'));
    }
    try {
      final user = await _userApi.updateUser(
        current.user.id,
        displayName: displayName,
        hasTrackingConsent: hasTrackingConsent,
      );
      _emit(Authenticated(user));
      await _cachingClient.invalidatePattern('/users/');
      return Result.success(user);
    } on Exception catch (e) {
      return Result.failure(e);
    }
  }

  Future<Result<void>> logout() async {
    await _runLogoutHooks();
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
    final current = _current;
    if (current is! Authenticated) {
      return Result.failure(StateError('No active session.'));
    }
    await _runLogoutHooks();
    try {
      await _userApi.deleteUser(current.user.id);
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
    final current = _current;
    if (current is! Authenticated) {
      return Result.failure(StateError('No active session.'));
    }
    try {
      await _userApi.requestDataExport(current.user.id);
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

  Future<Result<AuthSession>> _establishSession(
    Future<AuthSession> Function() call,
  ) async {
    try {
      final session = await call();
// ctx:auth_2fa_email:begin
      if (session.requiresTwoFactor) {
        return Result.success(session);
      }
// ctx:auth_2fa_email:end
      await _secureStorage.writeTokens(
        accessToken: session.accessToken!,
        refreshToken: session.refreshToken!,
      );
      await _secureStorage.writeUserId(session.user.id);
      _emit(Authenticated(session.user));
      return Result.success(session);
    } on Exception catch (e) {
      return Result.failure(e);
    }
  }

// ctx:auth_2fa_email:begin
  Future<void> establishSessionWith(AuthSession session) async {
    await _secureStorage.writeTokens(
      accessToken: session.accessToken!,
      refreshToken: session.refreshToken!,
    );
    await _secureStorage.writeUserId(session.user.id);
    _emit(Authenticated(session.user));
  }
// ctx:auth_2fa_email:end

  void dispose() => _controller.close();
}
