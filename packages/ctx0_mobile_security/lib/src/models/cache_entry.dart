import 'package:hive/hive.dart';

/// A cached HTTP response persisted by [HiveCacheService]
/// (docs/CACHING_IMPLEMENTATION.md).
class CacheEntry {
  const CacheEntry({
    required this.body,
    required this.statusCode,
    required this.timestamp,
    required this.headers,
  });

  final String body;
  final int statusCode;
  final DateTime timestamp;
  final Map<String, String> headers;

  bool isFresh(Duration ttl, {DateTime? now}) =>
      (now ?? DateTime.now()).difference(timestamp) < ttl;
}

/// Hand-written adapter — the entry is small and stable, codegen would be
/// overkill for four fields.
class CacheEntryAdapter extends TypeAdapter<CacheEntry> {
  @override
  final int typeId = 0;

  @override
  CacheEntry read(BinaryReader reader) {
    return CacheEntry(
      body: reader.readString(),
      statusCode: reader.readInt(),
      timestamp: DateTime.fromMillisecondsSinceEpoch(reader.readInt()),
      headers: Map<String, String>.from(reader.readMap()),
    );
  }

  @override
  void write(BinaryWriter writer, CacheEntry obj) {
    writer
      ..writeString(obj.body)
      ..writeInt(obj.statusCode)
      ..writeInt(obj.timestamp.millisecondsSinceEpoch)
      ..writeMap(obj.headers);
  }
}
