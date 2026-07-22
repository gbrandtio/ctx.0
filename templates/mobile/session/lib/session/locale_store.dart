import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Persists the language the user picked, if any.
///
/// Absent means "follow the device language" — the default. Only the language
/// code is stored (e.g. `el`), never a resolved locale, so a workspace that
/// later drops a language simply falls back instead of showing a dead choice.
abstract class LocaleStore {
  Future<String?> read();
  Future<void> write(String languageCode);
  Future<void> clear();
}

/// [LocaleStore] backed by platform secure storage — the same store the rest of
/// the session already uses, so the choice survives restarts without pulling in a
/// second persistence dependency.
class SecureLocaleStore implements LocaleStore {
  SecureLocaleStore([FlutterSecureStorage? storage]) : _storage = storage ?? const FlutterSecureStorage();

  final FlutterSecureStorage _storage;
  static const String _key = 'ctx.l10n.locale';

  @override
  Future<String?> read() => _storage.read(key: _key);

  @override
  Future<void> write(String languageCode) => _storage.write(key: _key, value: languageCode);

  @override
  Future<void> clear() => _storage.delete(key: _key);
}
