import 'package:firebase_messaging/firebase_messaging.dart';
import 'package:flutter/foundation.dart';

/// Thin wrapper over FirebaseMessaging so the repository stays testable.
/// Returns null when push is unavailable (permission denied, Firebase not
/// configured) — the app must keep working without push.
class PushTokenService {
  FirebaseMessaging get _messaging => FirebaseMessaging.instance;

  /// Requests permission (iOS prompt; Android 13+ runtime permission) and
  /// returns the device token, or null if unavailable.
  Future<String?> requestToken() async {
    try {
      final settings = await _messaging.requestPermission();
      if (settings.authorizationStatus == AuthorizationStatus.denied) {
        return null;
      }
      return await _messaging.getToken();
    } on Exception catch (e) {
      // Missing google-services.json / GoogleService-Info.plist: the
      // template runs without push until Firebase is configured.
      debugPrint('Push token unavailable: $e');
      return null;
    }
  }

  /// FCM rotates tokens; every new value must be re-registered.
  Stream<String> get onTokenRefresh => _messaging.onTokenRefresh;

  /// Foreground messages, for in-app surfacing.
  Stream<RemoteMessage> get onForegroundMessage =>
      FirebaseMessaging.onMessage;

  Future<void> deleteToken() async {
    try {
      await _messaging.deleteToken();
    } on Exception catch (e) {
      debugPrint('Push token deletion failed: $e');
    }
  }
}
