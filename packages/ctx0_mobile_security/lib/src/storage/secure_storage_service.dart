import 'dart:convert';
import 'dart:math';
import 'dart:typed_data';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Hardware-backed storage for secrets: tokens and the device signing key
/// (docs/SECURITY.md §2, §5). Nothing sensitive ever goes to
/// SharedPreferences or the Hive cache.
class SecureStorageService {
  SecureStorageService([FlutterSecureStorage? storage])
      : _storage = storage ??
            const FlutterSecureStorage(
              aOptions: AndroidOptions(encryptedSharedPreferences: true),
            );

  final FlutterSecureStorage _storage;

  static const _accessTokenKey = 'access_token';
  static const _refreshTokenKey = 'refresh_token';
  static const _userIdKey = 'user_id';
  static const _deviceIdKey = 'device_id';
  static const _devicePrivateKeyKey = 'device_private_key';
  static const _cacheEncryptionKeyKey = 'cache_encryption_key';

  /// The authenticated user's id, needed to address `/users/{id}` on
  /// session restore. Not a secret, but its lifecycle matches the tokens.
  Future<String?> readUserId() => _storage.read(key: _userIdKey);
  Future<void> writeUserId(String id) =>
      _storage.write(key: _userIdKey, value: id);

  Future<String?> readAccessToken() => _storage.read(key: _accessTokenKey);
  Future<String?> readRefreshToken() => _storage.read(key: _refreshTokenKey);

  Future<void> writeTokens({
    required String accessToken,
    required String refreshToken,
  }) async {
    await _storage.write(key: _accessTokenKey, value: accessToken);
    await _storage.write(key: _refreshTokenKey, value: refreshToken);
  }

  Future<void> deleteTokens() async {
    await _storage.delete(key: _accessTokenKey);
    await _storage.delete(key: _refreshTokenKey);
    await _storage.delete(key: _userIdKey);
  }

  Future<String?> readDeviceId() => _storage.read(key: _deviceIdKey);
  Future<void> writeDeviceId(String id) =>
      _storage.write(key: _deviceIdKey, value: id);

  /// The device's ECDSA P-256 private scalar (32 bytes, Base64-encoded at
  /// rest). Returned as bytes so callers can zero them after use
  /// (docs/SECURITY.md §2 "Zero Memory" hygiene).
  Future<Uint8List?> readDevicePrivateKey() async {
    final b64 = await _storage.read(key: _devicePrivateKeyKey);
    return b64 == null ? null : base64Decode(b64);
  }

  Future<void> writeDevicePrivateKey(Uint8List bytes) =>
      _storage.write(key: _devicePrivateKeyKey, value: base64Encode(bytes));

  /// Returns the 256-bit key that encrypts the on-device HTTP cache
  /// (docs/CACHING_IMPLEMENTATION.md), generating and persisting one on
  /// first use. Kept in hardware-backed storage so cached PII is never at
  /// rest in the clear.
  Future<Uint8List> readOrCreateCacheEncryptionKey() async {
    final existing = await _storage.read(key: _cacheEncryptionKeyKey);
    if (existing != null) {
      return base64Decode(existing);
    }
    final rnd = Random.secure();
    final key = Uint8List.fromList(
      List<int>.generate(32, (_) => rnd.nextInt(256)),
    );
    await _storage.write(
      key: _cacheEncryptionKeyKey,
      value: base64Encode(key),
    );
    return key;
  }

  /// Full purge — GDPR delete account (docs/APP_SHELL.md §4). Deliberately
  /// also removes the device identity; a fresh key pair is generated and
  /// re-registered on next launch.
  Future<void> clearAll() => _storage.deleteAll();
}
