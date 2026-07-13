part of 'settings_cubit.dart';

sealed class SettingsState extends Equatable {
  const SettingsState();

  @override
  List<Object?> get props => [];
}

final class SettingsIdle extends SettingsState {
  const SettingsIdle();
}

final class SettingsWorking extends SettingsState {
  const SettingsWorking();
}

final class SettingsExportRequested extends SettingsState {
  const SettingsExportRequested();
}

final class SettingsFailure extends SettingsState {
  const SettingsFailure(this.message);
  final String message;

  @override
  List<Object?> get props => [message];
}
