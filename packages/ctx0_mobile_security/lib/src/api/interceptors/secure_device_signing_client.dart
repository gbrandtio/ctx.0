import 'dart:convert';
import 'dart:math';
import 'dart:typed_data';

import 'package:http/http.dart' as http;

import '../../security/ctx_security_config.dart';
import '../../security/device_identity_service.dart';
import 'http_interceptor_utils.dart';

/// Request-signing interceptor (docs/HTTP_HANDLING.md "Request Signing",
/// docs/SECURITY.md §4.2). Sits ABOVE the AleClient so the signature is
/// computed over the plaintext body — the server decrypts first, then
/// verifies against the recovered plaintext.
///
/// Protocol 1.1: the canonical string is
/// METHOD|PATH?QUERY|TIMESTAMP|NONCE|BODY — the query string is signed so
/// it cannot be tampered in transit, and a fresh per-request nonce (echoed
/// in the signature header as `timestamp:nonce:signature`) lets the server
/// reject replays within the timestamp window.
///
/// Also implements self-healing registration: a 401 "Device not
/// registered." triggers POST /v1/security/app-instances (through the
/// inner chain, so it is ALE-encrypted) and one retry of the original
/// request.
class SecureDeviceSigningClient extends http.BaseClient {
  SecureDeviceSigningClient(this._inner, this._identity, this._config);

  static const String _notRegisteredMessage = 'Device not registered.';

  final http.Client _inner;
  final DeviceIdentityService _identity;
  final CtxSecurityConfig _config;

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    if (request is! http.Request) {
      // Streamed/multipart bodies cannot be canonicalized for signing;
      // all API traffic uses plain requests.
      return _inner.send(request);
    }

    final streamed = await _inner.send(_signed(request));
    if (streamed.statusCode != 401) return streamed;

    final response = await HttpInterceptorUtils.buffer(streamed);
    if (!response.body.contains(_notRegisteredMessage)) {
      return HttpInterceptorUtils.toStreamed(response);
    }

    await _registerAppInstance();
    // Re-sign with a fresh timestamp and retry once.
    return _inner.send(_signed(HttpInterceptorUtils.copyRequest(request)));
  }

  static final Random _random = Random.secure();

  static String _generateNonce() {
    final bytes = Uint8List(16);
    for (var i = 0; i < bytes.length; i++) {
      bytes[i] = _random.nextInt(256);
    }
    return base64Encode(bytes);
  }

  http.Request _signed(http.Request request) {
    final timestamp =
        (DateTime.now().millisecondsSinceEpoch ~/ 1000).toString();
    final nonce = _generateNonce();
    final query = request.url.query;
    final pathAndQuery = request.url.path.toLowerCase() +
        (query.isEmpty ? '' : '?$query');
    final canonical = '${request.method.toUpperCase()}'
        '|$pathAndQuery'
        '|$timestamp'
        '|$nonce'
        '|${utf8.decode(request.bodyBytes)}';
    final signature = _identity.sign(canonical);
    return request
      ..headers[_config.deviceIdHeader] = _identity.deviceId
      ..headers[_config.signatureHeader] = '$timestamp:$nonce:$signature';
  }

  Future<void> _registerAppInstance() async {
    final registration = http.Request(
      'POST',
      _config.resolveUri(_config.appInstancesPath),
    )
      ..headers['Content-Type'] = 'application/json'
      ..headers[_config.deviceIdHeader] = _identity.deviceId
      ..body = jsonEncode({
        'deviceId': _identity.deviceId,
        'publicKey': _identity.publicKeyBase64,
      });

    final response = await _inner.send(registration);
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw http.ClientException(
        'App instance registration failed: ${response.statusCode}',
        registration.url,
      );
    }
  }

  @override
  void close() => _inner.close();
}
