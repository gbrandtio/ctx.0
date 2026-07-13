import 'dart:async';

import 'package:flutter/widgets.dart' show Locale;
import 'package:flutter_bloc/flutter_bloc.dart';

import '../../data/repositories/auth_repository.dart';
import '../../data/services/storage/prefs_service.dart';

/// Global locale holder; `null` means "follow the system locale".
/// Persists to prefs and resets on logout (docs/SECURITY.md §5).
class LocaleCubit extends Cubit<Locale?> {
  LocaleCubit({
    required PrefsService prefs,
    required AuthRepository authRepository,
  }) : _prefs = prefs,
       super(_load(prefs)) {
    _authSubscription = authRepository.authStateChanges.listen((state) {
      if (state is Unauthenticated) emit(null);
    });
  }

  final PrefsService _prefs;
  late final StreamSubscription<AuthState> _authSubscription;

  static Locale? _load(PrefsService prefs) {
    final code = prefs.locale;
    return code == null ? null : Locale(code);
  }

  Future<void> setLocale(Locale? locale) async {
    emit(locale);
    if (locale != null) await _prefs.setLocale(locale.languageCode);
  }

  @override
  Future<void> close() async {
    await _authSubscription.cancel();
    return super.close();
  }
}
