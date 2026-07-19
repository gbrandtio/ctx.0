import 'dart:io' show Platform;

import 'package:firebase_core/firebase_core.dart';
import 'package:firebase_messaging/firebase_messaging.dart';

import 'notifications_repository.dart';

/// Registers this device for push delivery with the API.
///
/// Best-effort by design: if Firebase is not configured for the current platform
/// (see the feature's setup steps), registration is skipped so the rest of the app
/// keeps working and in-app notifications still function. Real delivery needs the
/// platform Firebase config files plus the API-side FCM credentials.
class PushService {
  const PushService();

  Future<void> register(NotificationsRepository repository) async {
    try {
      await Firebase.initializeApp();
      final messaging = FirebaseMessaging.instance;
      await messaging.requestPermission();
      final token = await messaging.getToken();
      if (token == null) return;
      await repository.registerDevice(_platform(), token);
    } catch (_) {
      // Firebase not configured on this platform/build: skip push registration.
    }
  }

  String _platform() {
    if (Platform.isIOS) return 'ios';
    if (Platform.isAndroid) return 'android';
    return 'web';
  }
}
