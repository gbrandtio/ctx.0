import 'dart:io';

import 'package:ctx0_mobile_security/ctx0_mobile_security.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:hive/hive.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

void main() {
  late Directory tempDir;
  late HiveCacheService cache;
  late int networkCalls;

  MockClient network({int status = 200}) => MockClient((request) async {
        networkCalls++;
        return http.Response('{"ok":true}', status, request: request);
      });

  setUp(() async {
    tempDir = await Directory.systemTemp.createTemp('hive_cache_test');
    cache = HiveCacheService();
    await cache.init(directory: tempDir.path);
    networkCalls = 0;
  });

  tearDown(() async {
    await Hive.deleteFromDisk();
    await tempDir.delete(recursive: true);
  });

  test('serves a fresh GET from cache with the x-from-cache marker',
      () async {
    final client = CachingClient(network(), cache);
    final url = Uri.parse('https://api.example.com/v1/items');

    await client.get(url); // network, then cached
    final second = await client.get(url);

    expect(networkCalls, 1);
    expect(second.headers[CachingClient.fromCacheHeader], 'true');
    expect(second.body, '{"ok":true}');
  });

  test('X-Bypass-Cache forces the network and never reaches the server',
      () async {
    late Map<String, String> seenHeaders;
    final client = CachingClient(
      MockClient((request) async {
        networkCalls++;
        seenHeaders = request.headers;
        return http.Response('{}', 200, request: request);
      }),
      cache,
    );
    final url = Uri.parse('https://api.example.com/v1/items');

    await client.get(url);
    await client.get(url, headers: {CachingClient.bypassHeader: 'true'});

    expect(networkCalls, 2, reason: 'bypass must skip the cached copy');
    expect(
      seenHeaders.containsKey(CachingClient.bypassHeader.toLowerCase()),
      isFalse,
      reason: 'the bypass header is client-side only',
    );
  });

  test('expired entries are refetched (TTL)', () async {
    final client =
        CachingClient(network(), cache, ttl: const Duration(seconds: 0));
    final url = Uri.parse('https://api.example.com/v1/items');

    await client.get(url);
    await client.get(url);

    expect(networkCalls, 2);
  });

  test('non-2xx responses and non-GET methods are not cached', () async {
    final failing = CachingClient(network(status: 500), cache);
    final url = Uri.parse('https://api.example.com/v1/items');
    await failing.get(url);
    await failing.get(url);
    expect(networkCalls, 2);

    networkCalls = 0;
    final posting = CachingClient(network(), cache);
    await posting.post(url, body: 'x');
    await posting.post(url, body: 'x');
    expect(networkCalls, 2);
  });

  test('invalidatePattern removes matching URLs only', () async {
    final client = CachingClient(network(), cache);
    final users = Uri.parse('https://api.example.com/v1/users/42');
    final items = Uri.parse('https://api.example.com/v1/items');

    await client.get(users);
    await client.get(items);
    await client.invalidatePattern('/users/42');
    networkCalls = 0;

    await client.get(users);
    await client.get(items);
    expect(networkCalls, 1, reason: 'only the invalidated URL refetches');
  });
}
