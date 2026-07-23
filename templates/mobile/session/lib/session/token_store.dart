import 'dart:async';
import 'dart:convert';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:http/http.dart' as http;

/// Stores the JWT access + refresh tokens for the signed-in session, and hands
/// out an access token that is still valid.
abstract class TokenStore {
  /// A usable access token, or null when there is no session.
  Future<String?> readAccessToken();
  Future<String?> readRefreshToken();

  /// When the stored access token expires, or null if nothing is stored.
  Future<DateTime?> readAccessExpiry();
  Future<void> save({
    required String accessToken,
    required String refreshToken,
    required DateTime accessExpiresAt,
  });
  Future<void> clear();

  /// Fires when the session ends without the user asking, i.e. the refresh
  /// token was rejected. [SessionCubit] listens and returns the app to the gate.
  Stream<void> get sessionLost;
}

/// Persists tokens in the platform secure storage.
class SecureTokenStore implements TokenStore {
  SecureTokenStore([FlutterSecureStorage? storage])
    : _storage = storage ?? const FlutterSecureStorage();

  final FlutterSecureStorage _storage;
  static const String _accessKey = 'ctx.auth.access';
  static const String _refreshKey = 'ctx.auth.refresh';
  static const String _expiresKey = 'ctx.auth.expires';

  @override
  Future<String?> readAccessToken() => _storage.read(key: _accessKey);

  @override
  Future<String?> readRefreshToken() => _storage.read(key: _refreshKey);

  @override
  Future<DateTime?> readAccessExpiry() async {
    final raw = await _storage.read(key: _expiresKey);
    return raw == null ? null : DateTime.tryParse(raw)?.toUtc();
  }

  @override
  Future<void> save({
    required String accessToken,
    required String refreshToken,
    required DateTime accessExpiresAt,
  }) async {
    await _storage.write(key: _accessKey, value: accessToken);
    await _storage.write(key: _refreshKey, value: refreshToken);
    await _storage.write(
      key: _expiresKey,
      value: accessExpiresAt.toUtc().toIso8601String(),
    );
  }

  @override
  Future<void> clear() async {
    await _storage.delete(key: _accessKey);
    await _storage.delete(key: _refreshKey);
    await _storage.delete(key: _expiresKey);
  }

  @override
  Stream<void> get sessionLost => const Stream<void>.empty();
}

/// Keeps the access token fresh. Reads pass through to [inner] while the stored
/// token has life left; once it is inside [_skew] of expiry the refresh token is
/// spent on `/v1/auth/refresh` and the rotated pair replaces it.
///
/// Rotation revokes the presented token server-side and treats a replay as
/// theft, which revokes the whole family. Two features refreshing at once would
/// therefore end the session, so refreshes are single-flight through
/// [_inFlight] and the app shares one instance of this class ([ctxSession]).
class RefreshingTokenStore implements TokenStore {
  RefreshingTokenStore(this._inner, {String? baseUrl, http.Client? client})
    : _baseUrl =
          baseUrl ??
          const String.fromEnvironment(
            'CTX_API_BASE_URL',
            defaultValue: 'http://localhost:5080',
          ),
      _http = client ?? http.Client();

  final TokenStore _inner;
  final String _baseUrl;
  final http.Client _http;
  final StreamController<void> _lost = StreamController<void>.broadcast();

  /// Renew this long before expiry, to cover clock skew and request latency.
  static const Duration _skew = Duration(seconds: 30);

  Future<String?>? _inFlight;

  @override
  Future<String?> readAccessToken() async {
    final access = await _inner.readAccessToken();
    if (access == null) return null;
    if (await _inner.readRefreshToken() == null) return access;

    final expiry = await _inner.readAccessExpiry();
    // No expiry recorded means a session stored before expiries were tracked;
    // treat it as due rather than handing out a token that may be dead.
    if (expiry != null &&
        DateTime.now().toUtc().isBefore(expiry.subtract(_skew))) {
      return access;
    }
    return _refresh();
  }

  @override
  Future<String?> readRefreshToken() => _inner.readRefreshToken();

  @override
  Future<DateTime?> readAccessExpiry() => _inner.readAccessExpiry();

  @override
  Future<void> save({
    required String accessToken,
    required String refreshToken,
    required DateTime accessExpiresAt,
  }) => _inner.save(
    accessToken: accessToken,
    refreshToken: refreshToken,
    accessExpiresAt: accessExpiresAt,
  );

  @override
  Future<void> clear() => _inner.clear();

  @override
  Stream<void> get sessionLost => _lost.stream;

  /// Rotate the refresh token, at most once concurrently.
  Future<String?> _refresh() {
    final existing = _inFlight;
    if (existing != null) return existing;
    final started = _rotate().whenComplete(() => _inFlight = null);
    _inFlight = started;
    return started;
  }

  Future<String?> _rotate() async {
    final refresh = await _inner.readRefreshToken();
    if (refresh == null) return null;

    final http.Response response;
    try {
      response = await _http.post(
        Uri.parse('$_baseUrl/v1/auth/refresh'),
        headers: const {'Content-Type': 'application/json'},
        body: jsonEncode({'refreshToken': refresh}),
      );
    } catch (_) {
      // Unreachable API. Keep the session; the next read tries again.
      return null;
    }

    if (response.statusCode == 401) {
      // Expired, unknown, or a replay the API answered by revoking the family.
      await _inner.clear();
      if (!_lost.isClosed) _lost.add(null);
      return null;
    }
    if (response.statusCode >= 400) return null;

    final json = jsonDecode(response.body) as Map<String, dynamic>;
    final access = json['accessToken'] as String;
    await _inner.save(
      accessToken: access,
      refreshToken: json['refreshToken'] as String,
      accessExpiresAt: DateTime.parse(
        json['accessTokenExpiresAt'] as String,
      ).toUtc(),
    );
    return access;
  }
}

/// The app-wide session credentials. Every repository that sends a bearer token
/// reads through this one instance, which is what keeps rotation single-flight.
final TokenStore ctxSession = RefreshingTokenStore(SecureTokenStore());
