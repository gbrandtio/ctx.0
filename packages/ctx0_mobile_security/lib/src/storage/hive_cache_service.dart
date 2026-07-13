import 'package:hive_flutter/hive_flutter.dart';

import '../models/cache_entry.dart';

/// Encapsulates all Hive operations for the HTTP cache
/// (docs/CACHING_IMPLEMENTATION.md). Only non-sensitive GET responses are
/// stored here.
class HiveCacheService {
  static const _boxName = 'http_cache';

  Box<CacheEntry>? _box;

  /// [directory] overrides the default app-documents location (tests).
  Future<void> init({String? directory}) async {
    if (directory != null) {
      Hive.init(directory);
    } else {
      await Hive.initFlutter();
    }
    if (!Hive.isAdapterRegistered(CacheEntryAdapter().typeId)) {
      Hive.registerAdapter(CacheEntryAdapter());
    }
    _box = await Hive.openBox<CacheEntry>(_boxName);
  }

  Box<CacheEntry> get _requireBox {
    final box = _box;
    if (box == null) {
      throw StateError('HiveCacheService.init() must be called at bootstrap');
    }
    return box;
  }

  CacheEntry? get(String key) => _requireBox.get(key);

  Future<void> put(String key, CacheEntry entry) =>
      _requireBox.put(key, entry);

  Future<void> delete(String key) => _requireBox.delete(key);

  /// Targeted invalidation for event-driven consistency: removes every
  /// cached URL containing [pattern] (e.g. '/users/42').
  Future<void> deleteWhereKeyContains(String pattern) async {
    final keys = _requireBox.keys
        .whereType<String>()
        .where((k) => k.contains(pattern))
        .toList();
    await _requireBox.deleteAll(keys);
  }

  /// Full cache purge — logout and GDPR account deletion.
  Future<void> clear() => _requireBox.clear();
}
