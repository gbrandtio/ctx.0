import 'dart:convert';
import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import 'package:ctxapp/features/auth/data/auth_repository.dart';
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

void main() {
  group('HttpAuthRepository', () {
    test('login stores the returned tokens and their expiry', () async {
      final store = InMemoryTokenStore();
      final expiry = DateTime.now().toUtc().add(const Duration(minutes: 15));
      final client = MockClient((request) async {
        expect(request.url.path, '/v1/auth/login');
        return http.Response(
          jsonEncode({
            'accessToken': 'jwt-abc',
            'accessTokenExpiresAt': expiry.toIso8601String(),
            'refreshToken': 'refresh-xyz',
          }),
          200,
        );
      });
      final repo = HttpAuthRepository(store, baseUrl: 'http://api', client: client);

      await repo.login('a@b.com', 'password1');

      expect(store.access, 'jwt-abc');
      expect(store.refresh, 'refresh-xyz');
      expect(store.expires, expiry);
      expect(await repo.hasSession(), isTrue);
    });

    test('a failure surfaces the server error and stores nothing', () async {
      final store = InMemoryTokenStore();
      final client = MockClient((request) async =>
          http.Response(jsonEncode({'error': 'Invalid credentials.'}), 401));
      final repo = HttpAuthRepository(store, baseUrl: 'http://api', client: client);

      await expectLater(
        repo.login('a@b.com', 'wrong'),
        throwsA(isA<AuthException>().having((e) => e.message, 'message', 'Invalid credentials.')),
      );
      expect(await repo.hasSession(), isFalse);
    });

    test('logout revokes the family server-side, then clears', () async {
      final store = signedIn(inMinutes: 10);
      String? revoked;
      final client = MockClient((request) async {
        expect(request.url.path, '/v1/auth/logout');
        revoked = (jsonDecode(request.body) as Map<String, dynamic>)['refreshToken'] as String;
        return http.Response('', 204);
      });
      final repo = HttpAuthRepository(store, baseUrl: 'http://api', client: client);

      await repo.logout();

      expect(revoked, 'refresh-old');
      expect(await repo.hasSession(), isFalse);
    });

    test('logout still ends the local session when the API is unreachable', () async {
      final store = signedIn(inMinutes: 10);
      final client = MockClient((_) async => throw const SocketException('offline'));
      final repo = HttpAuthRepository(store, baseUrl: 'http://api', client: client);

      await repo.logout();

      expect(store.access, isNull);
      expect(store.refresh, isNull);
    });
  });
}
