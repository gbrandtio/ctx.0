import 'dart:convert';

import 'package:http/http.dart' as http;

import '../../security/ctx_security_config.dart';
import '../../storage/secure_storage_service.dart';
import 'http_interceptor_utils.dart';

/// Attaches the bearer token and transparently refreshes an expired
/// session (docs/ERROR_HANDLING.md §4 — "401 handled automatically by the
/// AuthRefreshClient"). On 401 it performs a single-flight token refresh
/// (rotating refresh tokens) and retries the original request once; if
/// the refresh itself fails, tokens are purged and [onSessionExpired]
/// fires so the auth state stream can push the user to login.
class AuthRefreshClient extends http.BaseClient {
  AuthRefreshClient(
    this._inner,
    this._secureStorage,
    this._config, {
    this.onSessionExpired,
  });

  final http.Client _inner;
  final SecureStorageService _secureStorage;
  final CtxSecurityConfig _config;
  final void Function()? onSessionExpired;

  /// In-flight refresh shared by concurrent 401s (single-flight).
  Future<bool>? _refreshing;

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    final token = await _secureStorage.readAccessToken();
    if (token != null) {
      request.headers['Authorization'] = 'Bearer $token';
    }

    final streamed = await _inner.send(request);
    if (streamed.statusCode != 401 ||
        request is! http.Request ||
        _isAuthEndpoint(request.url)) {
      return streamed;
    }

    // Buffer so the original 401 can still be returned if refresh fails.
    final response = await HttpInterceptorUtils.buffer(streamed);
    final refreshed = await (_refreshing ??= _refreshTokens()
        .whenComplete(() => _refreshing = null));
    if (!refreshed) return HttpInterceptorUtils.toStreamed(response);

    final retry = HttpInterceptorUtils.copyRequest(request);
    final newToken = await _secureStorage.readAccessToken();
    if (newToken != null) {
      retry.headers['Authorization'] = 'Bearer $newToken';
    }
    return _inner.send(retry);
  }

  bool _isAuthEndpoint(Uri url) =>
      _config.authEndpointPaths.any(url.path.endsWith);

  Future<bool> _refreshTokens() async {
    final refreshToken = await _secureStorage.readRefreshToken();
    if (refreshToken == null) {
      _expireSession();
      return false;
    }

    try {
      final request = http.Request(
        'POST',
        _config.resolveUri(_config.refreshTokenPath),
      )
        ..headers['Content-Type'] = 'application/json'
        ..body = jsonEncode({'refreshToken': refreshToken});

      final response =
          await HttpInterceptorUtils.buffer(await _inner.send(request));
      if (response.statusCode < 200 || response.statusCode >= 300) {
        _expireSession();
        return false;
      }

      final json = jsonDecode(response.body) as Map<String, dynamic>;
      await _secureStorage.writeTokens(
        accessToken: json['accessToken'] as String,
        refreshToken: json['refreshToken'] as String,
      );
      return true;
    } on Exception {
      // Network failure during refresh: keep tokens, surface the original
      // 401; the next request will retry the refresh.
      return false;
    }
  }

  void _expireSession() {
    _secureStorage.deleteTokens();
    onSessionExpired?.call();
  }

  @override
  void close() => _inner.close();
}
