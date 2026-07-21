import 'package:bloc_test/bloc_test.dart';
import 'package:flutter/widgets.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:ctxapp/features/l10n/bloc/locale_cubit.dart';
import 'package:ctxapp/features/l10n/data/locale_store.dart';
import 'package:ctxapp/l10n/l10n_support.dart';

/// In-memory language storage, standing in for the device's secure storage.
class FakeLocaleStore implements LocaleStore {
  FakeLocaleStore([this._code]);

  String? _code;

  String? get code => _code;

  @override
  Future<String?> read() async => _code;

  @override
  Future<void> write(String languageCode) async => _code = languageCode;

  @override
  Future<void> clear() async => _code = null;
}

void main() {
  // The workspace always ships English; a second language is only present when
  // one was selected at create time.
  final Locale? second = AppL10nSupport.supportedLocales.length > 1
      ? AppL10nSupport.supportedLocales[1]
      : null;

  test('starts with no override, so the app follows the device language', () {
    final cubit = LocaleCubit(FakeLocaleStore());
    expect(cubit.state, isNull);
  });

  test('a stored language this build no longer ships is discarded', () async {
    final store = FakeLocaleStore('zz');
    final cubit = LocaleCubit(store);
    await cubit.load();

    expect(cubit.state, isNull);
    expect(store.code, isNull, reason: 'the stale choice is cleared, not kept');
  });

  test('the API client is told which language is in force', () async {
    final announced = <String>[];
    final cubit = LocaleCubit(FakeLocaleStore(), onLanguageChanged: announced.add);
    await cubit.load();

    expect(announced, isNotEmpty);
    expect(announced.last, isNotEmpty, reason: 'falls back to the device language');
  });

  if (second == null) return;

  blocTest<LocaleCubit, Locale?>(
    'selecting a language overrides the device and is persisted',
    build: () => LocaleCubit(FakeLocaleStore()),
    act: (cubit) => cubit.select(second),
    expect: () => [second],
  );

  test('a stored language is restored on the next start', () async {
    final store = FakeLocaleStore();
    await LocaleCubit(store).select(second);

    final restored = LocaleCubit(store);
    await restored.load();

    expect(restored.state, second);
  });

  blocTest<LocaleCubit, Locale?>(
    'handing the choice back to the device drops the override',
    build: () => LocaleCubit(FakeLocaleStore(second.languageCode)),
    act: (cubit) async {
      await cubit.load();
      await cubit.useDeviceLanguage();
    },
    expect: () => [second, null],
  );
}
