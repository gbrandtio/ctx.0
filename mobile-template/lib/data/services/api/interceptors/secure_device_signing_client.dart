import 'dart:convert';

import 'package:http/http.dart' as http;

import '../../../../core/constants/api_constants.dart';
import '../../security/device_identity_service.dart';
import 'http_interceptor_utils.dart';

/// Request-signing interceptor (docs/HTTP_HANDLING.md "Request Signing",
/// docs/SECURITY.md §4.2). Sits ABOVE the AleClient so the signature is
/// computed over the plaintext body — the server decrypts first, then
/// verifies against the recovered plaintext.
///
/// Also implements self-healing registration: a 401 "Device not
/// registered." triggers POST /v1/security/app-instances (through the
/// inner chain, so it is ALE-encrypted) and one retry of the original
/// request.
class SecureDeviceSigningClient extends http.BaseClient {
  SecureDeviceSigningClient(this._inner, this._identity);

  static const String deviceIdHeader = 'X-App-Device-Id';
  static const String signatureHeader = 'X-App-Signature';
  static const String _notRegisteredMessage = 'Device not registered.';

  final http.Client _inner;
  final DeviceIdentityService _identity;

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

  http.Request _signed(http.Request request) {
    final timestamp =
        (DateTime.now().millisecondsSinceEpoch ~/ 1000).toString();
    final canonical = '${request.method.toUpperCase()}'
        '|${request.url.path.toLowerCase()}'
        '|$timestamp'
        '|${utf8.decode(request.bodyBytes)}';
    final signature = _identity.sign(canonical);
    return request
      ..headers[deviceIdHeader] = _identity.deviceId
      ..headers[signatureHeader] = '$timestamp:$signature';
  }

  Future<void> _registerAppInstance() async {
    final registration = http.Request(
      'POST',
      ApiConstants.uri(ApiConstants.appInstances),
    )
      ..headers['Content-Type'] = 'application/json'
      ..headers[deviceIdHeader] = _identity.deviceId
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
