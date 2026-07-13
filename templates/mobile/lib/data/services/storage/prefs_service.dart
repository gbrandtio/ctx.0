import 'dart:async';

import 'package:shared_preferences/shared_preferences.dart';

/// Wraps SharedPreferences for non-sensitive UI preferences only
/// (docs/SECURITY.md §2 — never tokens or secrets; those belong in
/// [SecureStorageService]).
class PrefsService {
  PrefsService(this._prefs);

  static Future<PrefsService> create() async =>
      PrefsService(await SharedPreferences.getInstance());

  final SharedPreferences _prefs;

  static const _themeModeKey = 'theme_mode';
  static const _localeKey = 'locale';
  static const _onboardingDoneKey = 'onboarding_done';
  static const _hasSeenGdprBannerKey = 'has_seen_gdpr_banner';
  static const _trackingConsentKey = 'tracking_consent';

  final _trackingConsentController = StreamController<bool>.broadcast();

  /// Reactive stream enforcing GDPR privacy-by-design for third-party SDKs
  /// (docs/INTEGRATIONS.md & docs/APP_SHELL.md).
  Stream<bool> get trackingConsentChanges async* {
    yield trackingConsentGranted;
    yield* _trackingConsentController.stream;
  }

  String? get themeMode => _prefs.getString(_themeModeKey);
  Future<void> setThemeMode(String value) =>
      _prefs.setString(_themeModeKey, value);

  String? get locale => _prefs.getString(_localeKey);
  Future<void> setLocale(String value) => _prefs.setString(_localeKey, value);

  bool get onboardingDone => _prefs.getBool(_onboardingDoneKey) ?? false;
  Future<void> setOnboardingDone(bool value) =>
      _prefs.setBool(_onboardingDoneKey, value);

  bool get hasSeenGdprBanner => _prefs.getBool(_hasSeenGdprBannerKey) ?? false;
  Future<void> setHasSeenGdprBanner(bool value) =>
      _prefs.setBool(_hasSeenGdprBannerKey, value);

  bool get trackingConsentGranted =>
      _prefs.getBool(_trackingConsentKey) ?? false;
  Future<void> setTrackingConsentGranted(bool value) async {
    await _prefs.setBool(_trackingConsentKey, value);
    _trackingConsentController.add(value);
  }

  /// Full purge — used by the GDPR delete-account flow (docs/APP_SHELL.md §4)
  /// and on logout to prevent preference leakage to the next user.
  Future<void> clear() => _prefs.clear();
}
