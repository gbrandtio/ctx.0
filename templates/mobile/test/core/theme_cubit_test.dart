import 'dart:async';

import 'package:app_template/core/theme/theme_cubit.dart';
import 'package:app_template/data/repositories/auth_repository.dart';
import 'package:app_template/data/services/storage/prefs_service.dart';
import 'package:flutter/material.dart' show ThemeMode;
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

class _MockPrefs extends Mock implements PrefsService {}

class _MockAuthRepository extends Mock implements AuthRepository {}

void main() {
  late _MockPrefs prefs;
  late _MockAuthRepository authRepository;
  late StreamController<AuthState> authStates;

  setUp(() {
    prefs = _MockPrefs();
    authRepository = _MockAuthRepository();
    authStates = StreamController<AuthState>.broadcast();
    when(
      () => authRepository.authStateChanges,
    ).thenAnswer((_) => authStates.stream);
    when(() => prefs.setThemeMode(any())).thenAnswer((_) async {});
  });

  tearDown(() => authStates.close());

  test('restores the persisted mode', () {
    when(() => prefs.themeMode).thenReturn('dark');

    final cubit = ThemeCubit(prefs: prefs, authRepository: authRepository);

    expect(cubit.state, ThemeMode.dark);
  });

  test('setMode persists the choice', () async {
    when(() => prefs.themeMode).thenReturn(null);
    final cubit = ThemeCubit(prefs: prefs, authRepository: authRepository);

    await cubit.setMode(ThemeMode.light);

    expect(cubit.state, ThemeMode.light);
    verify(() => prefs.setThemeMode('light')).called(1);
  });

  test(
    'resets to system on logout so preferences never leak to the next user',
    () async {
      when(() => prefs.themeMode).thenReturn('dark');
      final cubit = ThemeCubit(prefs: prefs, authRepository: authRepository);

      authStates.add(const Unauthenticated());
      await Future<void>.delayed(Duration.zero);

      expect(cubit.state, ThemeMode.system);
    },
  );
}
