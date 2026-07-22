import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import 'package:ctxapp/session/token_store.dart';

/// In-memory token store for tests (no platform secure storage).
class InMemoryTokenStore implements TokenStore {
  String? access;
  String? refresh;
  DateTime? expires;

  @override
  Future<String?> readAccessToken() async => access;

  @override
  Future<String?> readRefreshToken() async => refresh;

  @override
  Future<DateTime?> readAccessExpiry() async => expires;

  @override
  Future<void> save({
    required String accessToken,
    required String refreshToken,
    required DateTime accessExpiresAt,
  }) async {
    access = accessToken;
    refresh = refreshToken;
    expires = accessExpiresAt;
  }

  @override
  Future<void> clear() async {
    access = null;
    refresh = null;
    expires = null;
  }

  @override
  Stream<void> get sessionLost => const Stream<void>.empty();
}

/// A signed-in store whose access token expires [inMinutes] from now.
InMemoryTokenStore signedIn({required int inMinutes}) => InMemoryTokenStore()
  ..access = 'jwt-old'
  ..refresh = 'refresh-old'
  ..expires = DateTime.now().toUtc().add(Duration(minutes: inMinutes));

String rotatedBody() => jsonEncode({
      'accessToken': 'jwt-new',
      'accessTokenExpiresAt': DateTime.now().toUtc().add(const Duration(minutes: 15)).toIso8601String(),
      'refreshToken': 'refresh-new',
      'refreshTokenExpiresAt': DateTime.now().toUtc().add(const Duration(days: 14)).toIso8601String(),
    });

void main() {
  group('RefreshingTokenStore', () {
    test('hands back a live token without calling the API', () async {
      final inner = signedIn(inMinutes: 10);
      var calls = 0;
      final store = RefreshingTokenStore(inner,
          baseUrl: 'http://api', client: MockClient((_) async {
        calls++;
        return http.Response('', 200);
      }));

      expect(await store.readAccessToken(), 'jwt-old');
      expect(calls, 0);
    });

    test('rotates an expired token and stores the new pair', () async {
      final inner = signedIn(inMinutes: -1);
      final store = RefreshingTokenStore(inner,
          baseUrl: 'http://api', client: MockClient((request) async {
        expect(request.url.path, '/v1/auth/refresh');
        expect((jsonDecode(request.body) as Map<String, dynamic>)['refreshToken'], 'refresh-old');
        return http.Response(rotatedBody(), 200);
      }));

      expect(await store.readAccessToken(), 'jwt-new');
      expect(inner.refresh, 'refresh-new');
      expect(inner.expires!.isAfter(DateTime.now().toUtc()), isTrue);
    });

    test('rotates a token that is inside the skew margin of expiry', () async {
      final inner = signedIn(inMinutes: 10)
        ..expires = DateTime.now().toUtc().add(const Duration(seconds: 5));
      final store = RefreshingTokenStore(inner,
          baseUrl: 'http://api', client: MockClient((_) async => http.Response(rotatedBody(), 200)));

      expect(await store.readAccessToken(), 'jwt-new');
    });

    test('rotates when no expiry was recorded', () async {
      final inner = signedIn(inMinutes: 10)..expires = null;
      final store = RefreshingTokenStore(inner,
          baseUrl: 'http://api', client: MockClient((_) async => http.Response(rotatedBody(), 200)));

      expect(await store.readAccessToken(), 'jwt-new');
    });

    test('concurrent reads rotate exactly once', () async {
      final inner = signedIn(inMinutes: -1);
      var calls = 0;
      final gate = Completer<void>();
      final store = RefreshingTokenStore(inner,
          baseUrl: 'http://api', client: MockClient((_) async {
        calls++;
        // Hold the first request open so the others arrive mid-flight, which is
        // the case that would replay the refresh token and kill the family.
        await gate.future;
        return http.Response(rotatedBody(), 200);
      }));

      final reads = Future.wait([
        store.readAccessToken(),
        store.readAccessToken(),
        store.readAccessToken(),
      ]);
      await Future<void>.delayed(Duration.zero);
      gate.complete();

      expect(await reads, ['jwt-new', 'jwt-new', 'jwt-new']);
      expect(calls, 1);
    });

    test('a rejected refresh clears the session and reports it lost', () async {
      final inner = signedIn(inMinutes: -1);
      final store = RefreshingTokenStore(inner,
          baseUrl: 'http://api',
          client: MockClient((_) async =>
              http.Response(jsonEncode({'error': 'Refresh token reuse detected.'}), 401)));

      final lost = expectLater(store.sessionLost, emits(anything));

      expect(await store.readAccessToken(), isNull);
      expect(inner.access, isNull);
      expect(inner.refresh, isNull);
      await lost;
    });

    test('an unreachable API keeps the session and reports nothing lost', () async {
      final inner = signedIn(inMinutes: -1);
      final store = RefreshingTokenStore(inner,
          baseUrl: 'http://api',
          client: MockClient((_) async => throw const SocketException('offline')));

      store.sessionLost.listen(expectAsync1((_) {}, count: 0));

      expect(await store.readAccessToken(), isNull);
      expect(inner.access, 'jwt-old');
      expect(inner.refresh, 'refresh-old');
    });

    test('no stored session reads as null without calling the API', () async {
      var calls = 0;
      final store = RefreshingTokenStore(InMemoryTokenStore(),
          baseUrl: 'http://api', client: MockClient((_) async {
        calls++;
        return http.Response('', 200);
      }));

      expect(await store.readAccessToken(), isNull);
      expect(calls, 0);
    });
  });
}
