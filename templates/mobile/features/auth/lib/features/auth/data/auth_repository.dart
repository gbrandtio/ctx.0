import 'dart:convert';

import 'package:http/http.dart' as http;

import 'token_store.dart';

/// Raised when authentication fails; carries the server's message.
class AuthException implements Exception {
  const AuthException(this.message);
  final String message;
  @override
  String toString() => message;
}

/// Authenticates against the API and manages the local session tokens.
abstract class AuthRepository {
  Future<void> login(String email, String password);
  Future<void> register(String email, String password);
  Future<void> logout();
  Future<bool> hasSession();
}

/// [AuthRepository] backed by the `/v1/auth` endpoints. These endpoints exchange
/// credentials over TLS for a JWT + refresh token, which are stored locally.
class HttpAuthRepository implements AuthRepository {
  HttpAuthRepository(this._store, {String? baseUrl, http.Client? client})
      : _baseUrl = baseUrl ?? const String.fromEnvironment('CTX_API_BASE_URL', defaultValue: 'http://localhost:5080'),
        _http = client ?? http.Client();

  final TokenStore _store;
  final String _baseUrl;
  final http.Client _http;

  @override
  Future<void> login(String email, String password) => _authenticate('/v1/auth/login', email, password);

  @override
  Future<void> register(String email, String password) => _authenticate('/v1/auth/register', email, password);

  @override
  Future<void> logout() => _store.clear();

  @override
  Future<bool> hasSession() async => (await _store.readAccessToken()) != null;

  Future<void> _authenticate(String path, String email, String password) async {
    final response = await _http.post(
      Uri.parse('$_baseUrl$path'),
      headers: const {'Content-Type': 'application/json'},
      body: jsonEncode({'email': email, 'password': password}),
    );
    if (response.statusCode >= 400) {
      throw AuthException(_errorMessage(response.body));
    }
    final json = jsonDecode(response.body) as Map<String, dynamic>;
    await _store.save(
      accessToken: json['accessToken'] as String,
      refreshToken: json['refreshToken'] as String,
    );
  }

  String _errorMessage(String body) {
    try {
      final json = jsonDecode(body);
      if (json is Map && json['error'] is String) {
        return json['error'] as String;
      }
    } on FormatException {
      // fall through to the default message
    }
    return 'Authentication failed';
  }
}
