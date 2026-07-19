import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import 'package:acme/features/auth/data/auth_repository.dart';
import 'package:acme/features/auth/data/token_store.dart';

/// In-memory token store for tests (no platform secure storage).
class InMemoryTokenStore implements TokenStore {
  String? access;
  String? refresh;

  @override
  Future<String?> readAccessToken() async => access;

  @override
  Future<void> save({required String accessToken, required String refreshToken}) async {
    access = accessToken;
    refresh = refreshToken;
  }

  @override
  Future<void> clear() async {
    access = null;
    refresh = null;
  }
}

void main() {
  test('login stores the returned tokens', () async {
    final store = InMemoryTokenStore();
    final client = MockClient((request) async {
      expect(request.url.path, '/v1/auth/login');
      return http.Response(jsonEncode({'accessToken': 'jwt-abc', 'refreshToken': 'refresh-xyz'}), 200);
    });
    final repo = HttpAuthRepository(store, baseUrl: 'http://api', client: client);

    await repo.login('a@b.com', 'password1');

    expect(store.access, 'jwt-abc');
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

  test('logout clears the session', () async {
    final store = InMemoryTokenStore()..access = 'jwt';
    final repo = HttpAuthRepository(store, baseUrl: 'http://api', client: MockClient((_) async => http.Response('', 200)));

    await repo.logout();

    expect(await repo.hasSession(), isFalse);
  });
}
