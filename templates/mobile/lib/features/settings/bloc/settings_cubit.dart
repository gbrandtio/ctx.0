import 'package:bloc/bloc.dart';
import 'package:equatable/equatable.dart';

import '../../../core/result/result.dart';
import '../../../core/utils/app_exception.dart';
import '../../../data/repositories/auth_repository.dart';
import '../../../data/services/storage/prefs_service.dart';

part 'settings_state.dart';

/// Drives the GDPR actions on the settings screen (docs/APP_SHELL.md §4).
/// Theme and language tiles talk to ThemeCubit/LocaleCubit directly.
class SettingsCubit extends Cubit<SettingsState> {
  SettingsCubit({
    required AuthRepository authRepository,
    required PrefsService prefs,
  }) : _authRepository = authRepository,
       _prefs = prefs,
       super(const SettingsIdle());

  final AuthRepository _authRepository;
  final PrefsService _prefs;

  Future<void> setTrackingConsent(bool granted) async {
    await _prefs.setTrackingConsentGranted(granted);
    if (granted) await _prefs.setHasSeenGdprBanner(true);

    if (state is SettingsWorking) return;
    emit(const SettingsWorking());
    final result = await _authRepository.updateProfile(
      hasTrackingConsent: granted,
    );
    switch (result) {
      case Success():
        emit(const SettingsIdle());
      case Failure(:final error):
        emit(SettingsFailure(AppException.from(error).userFriendlyMessage));
    }
  }

  /// Anonymizing server delete → full local purge → logout. Navigation to
  /// login happens via the auth stream redirect.
  Future<void> deleteAccount() async {
    if (state is SettingsWorking) return;
    emit(const SettingsWorking());
    final result = await _authRepository.deleteAccount();
    switch (result) {
      case Success():
        emit(const SettingsIdle());
      case Failure(:final error):
        emit(SettingsFailure(AppException.from(error).userFriendlyMessage));
    }
  }

  /// Server-side export request; delivery is notified via push.
  Future<void> requestDataExport() async {
    if (state is SettingsWorking) return;
    emit(const SettingsWorking());
    final result = await _authRepository.requestDataExport();
    switch (result) {
      case Success():
        emit(const SettingsExportRequested());
      case Failure(:final error):
        emit(SettingsFailure(AppException.from(error).userFriendlyMessage));
    }
  }
}
