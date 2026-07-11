import 'dart:async';

import 'package:flutter/material.dart' show ThemeMode;
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../data/repositories/auth_repository.dart';
import '../../data/services/storage/prefs_service.dart';

/// Global theme mode holder (docs/STATE_MANAGEMENT.md §1 — simple state,
/// so a Cubit). Persists to prefs and resets to the default on logout so
/// preferences never leak to the next user (docs/SECURITY.md §5).
class ThemeCubit extends Cubit<ThemeMode> {
  ThemeCubit({
    required PrefsService prefs,
    required AuthRepository authRepository,
  })  : _prefs = prefs,
        super(_load(prefs)) {
    _authSubscription = authRepository.authStateChanges.listen((state) {
      if (state is Unauthenticated) emit(ThemeMode.system);
    });
  }

  final PrefsService _prefs;
  late final StreamSubscription<AuthState> _authSubscription;

  static ThemeMode _load(PrefsService prefs) {
    return switch (prefs.themeMode) {
      'light' => ThemeMode.light,
      'dark' => ThemeMode.dark,
      _ => ThemeMode.system,
    };
  }

  Future<void> setMode(ThemeMode mode) async {
    emit(mode);
    await _prefs.setThemeMode(mode.name);
  }

  @override
  Future<void> close() async {
    await _authSubscription.cancel();
    return super.close();
  }
}
