import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Stores the JWT access + refresh tokens for the signed-in session.
abstract class TokenStore {
  Future<String?> readAccessToken();
  Future<void> save({required String accessToken, required String refreshToken});
  Future<void> clear();
}

/// Persists tokens in the platform secure storage.
class SecureTokenStore implements TokenStore {
  SecureTokenStore([FlutterSecureStorage? storage]) : _storage = storage ?? const FlutterSecureStorage();

  final FlutterSecureStorage _storage;
  static const String _accessKey = 'ctx.auth.access';
  static const String _refreshKey = 'ctx.auth.refresh';

  @override
  Future<String?> readAccessToken() => _storage.read(key: _accessKey);

  @override
  Future<void> save({required String accessToken, required String refreshToken}) async {
    await _storage.write(key: _accessKey, value: accessToken);
    await _storage.write(key: _refreshKey, value: refreshToken);
  }

  @override
  Future<void> clear() async {
    await _storage.delete(key: _accessKey);
    await _storage.delete(key: _refreshKey);
  }
}
