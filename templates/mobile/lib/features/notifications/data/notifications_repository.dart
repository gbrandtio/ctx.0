import 'dart:async';

import '../../../core/result/result.dart';
import '../../../data/repositories/auth_repository.dart';
import '../../../models/app_notification.dart';
import 'notification_api_service.dart';
import 'push_token_service.dart';

/// SSOT for push registration and the in-app feed
/// (templates/api/docs/features/NOTIFICATIONS.md). Registration follows
/// the auth stream: token registered while a session exists, unregistered
/// via a logout hook while the session is still valid.
class NotificationsRepository {
  NotificationsRepository({
    required NotificationApiService api,
    required PushTokenService pushTokens,
    required AuthRepository authRepository,
  })  : _api = api,
        _pushTokens = pushTokens {
    _authSubscription = authRepository.authStateChanges.listen((state) {
      if (state is Authenticated) unawaited(_registerToken());
    });
    _refreshSubscription = _pushTokens.onTokenRefresh.listen(
      (token) => unawaited(_registerRefreshedToken(token)),
    );
    authRepository.registerLogoutHook(_unregisterToken);
  }

  final NotificationApiService _api;
  final PushTokenService _pushTokens;

  late final StreamSubscription<AuthState> _authSubscription;
  late final StreamSubscription<String> _refreshSubscription;
  bool _registered = false;

  Future<void> _registerToken() async {
    if (_registered) return;
    try {
      final token = await _pushTokens.requestToken();
      if (token == null) return; // push unavailable — app works without it
      await _api.registerFirebaseToken(token);
      _registered = true;
    } on Exception {
      // Best-effort: a failed registration must never break login. The
      // next auth emission or token refresh retries.
    }
  }

  Future<void> _registerRefreshedToken(String token) async {
    try {
      await _api.registerFirebaseToken(token);
      _registered = true;
    } on Exception {
      _registered = false;
    }
  }

  Future<void> _unregisterToken() async {
    if (!_registered) return;
    _registered = false;
    await _api.unregisterFirebaseToken();
    await _pushTokens.deleteToken();
  }

  Future<Result<NotificationPage>> getFeed({required int page}) async {
    try {
      return Result.success(await _api.getNotifications(page: page));
    } on Exception catch (e) {
      return Result.failure(e);
    }
  }

  void dispose() {
    _authSubscription.cancel();
    _refreshSubscription.cancel();
  }
}
