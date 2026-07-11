import 'package:http/http.dart' as http;

import '../../../../core/models/cache_entry.dart';
import '../../storage/hive_cache_service.dart';
import 'http_interceptor_utils.dart';

/// Cache-First interceptor for GET requests (docs/CACHING_IMPLEMENTATION.md).
/// Outermost link of the chain: it must decide before any signing or
/// encryption work happens.
class CachingClient extends http.BaseClient {
  CachingClient(this._inner, this._cache, {this.ttl = defaultTtl});

  /// Default TTL (docs/CACHING_IMPLEMENTATION.md "Security & Performance").
  static const Duration defaultTtl = Duration(minutes: 15);

  static const String bypassHeader = 'X-Bypass-Cache';
  static const String fromCacheHeader = 'x-from-cache';

  final http.Client _inner;
  final HiveCacheService _cache;
  final Duration ttl;

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    final bypass = request.headers.remove(bypassHeader) != null;
    final cacheable = request.method == 'GET' && !bypass;
    final key = request.url.toString();

    if (cacheable) {
      final entry = _cache.get(key);
      if (entry != null && entry.isFresh(ttl)) {
        return HttpInterceptorUtils.toStreamed(
          http.Response(
            entry.body,
            entry.statusCode,
            headers: {...entry.headers, fromCacheHeader: 'true'},
            request: request,
          ),
        );
      }
    }

    final streamed = await _inner.send(request);
    if (request.method != 'GET' ||
        streamed.statusCode < 200 ||
        streamed.statusCode >= 300) {
      return streamed;
    }

    final response = await HttpInterceptorUtils.buffer(streamed);
    await _cache.put(
      key,
      CacheEntry(
        body: response.body,
        statusCode: response.statusCode,
        timestamp: DateTime.now(),
        headers: response.headers,
      ),
    );
    return HttpInterceptorUtils.toStreamed(response);
  }

  /// Event-driven invalidation: clear every cached URL matching [pattern]
  /// after a mutation that affects it.
  Future<void> invalidatePattern(String pattern) =>
      _cache.deleteWhereKeyContains(pattern);

  /// Full purge — logout / account deletion.
  Future<void> clearCache() => _cache.clear();

  @override
  void close() => _inner.close();
}
