import 'dart:ui' show PlatformDispatcher;

import 'package:flutter/widgets.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import 'package:ctxapp/l10n/l10n_support.dart';

import '../data/locale_store.dart';

/// Holds the app's locale override.
///
/// The state is the locale handed to `MaterialApp.locale`: `null` means no
/// override, which is what makes Flutter follow the device language. Anything
/// else is one of [AppL10nSupport.supportedLocales].
class LocaleCubit extends Cubit<Locale?> {
  LocaleCubit(this._store, {void Function(String languageCode)? onLanguageChanged})
      : _onLanguageChanged = onLanguageChanged,
        super(null);

  final LocaleStore _store;

  /// Notified with the language actually in force — the override when there is
  /// one, the device's language otherwise. The API client hangs off this to keep
  /// its `Accept-Language` header in step, so server messages arrive in the same
  /// language as the UI.
  final void Function(String languageCode)? _onLanguageChanged;

  /// Restore the stored choice. A code that this build no longer ships (the
  /// workspace was regenerated with fewer languages) is discarded, not honoured.
  Future<void> load() async {
    final code = await _store.read();
    final matches = code == null
        ? const <Locale>[]
        : AppL10nSupport.supportedLocales.where((locale) => locale.languageCode == code).toList();
    if (code != null && matches.isEmpty) {
      await _store.clear();
    }
    if (matches.isNotEmpty) emit(matches.first);
    _announce();
  }

  /// Override the device language. [locale] must be a supported locale.
  Future<void> select(Locale locale) async {
    await _store.write(locale.languageCode);
    emit(locale);
    _announce();
  }

  /// Drop the override and follow the device language again.
  Future<void> useDeviceLanguage() async {
    await _store.clear();
    emit(null);
    _announce();
  }

  /// Report the language in force: the override, or the device's own language
  /// when there is none (which is what the app is showing in that case).
  void _announce() {
    _onLanguageChanged?.call(state?.languageCode ?? PlatformDispatcher.instance.locale.languageCode);
  }
}
