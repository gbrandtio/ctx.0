import 'dart:convert';
import 'dart:typed_data';

import 'package:http/http.dart' as http;

import '../../security/crypto_utils.dart';
import '../security_metadata_service.dart';
import 'http_interceptor_utils.dart';

/// Application Layer Encryption interceptor (docs/SECURITY.md §1, §4;
/// docs/HTTP_HANDLING.md "Application Layer Encryption"). Innermost link
/// before the network: it must run AFTER signing so the signature covers
/// the plaintext ("Sign the Plaintext, Encrypt After", docs/SECURITY.md
/// §4.2).
class AleClient extends http.BaseClient {
  AleClient(this._inner, this._metadata);

  static const String enabledHeader = 'X-ALE-Enabled';
  static const String sessionKeyHeader = 'X-ALE-Session-Key';

  final http.Client _inner;
  final SecurityMetadataService _metadata;

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    // Only plain requests with a body are encrypted; body-less requests
    // (GETs) carry nothing to protect and get no session key.
    if (request is! http.Request || request.bodyBytes.isEmpty) {
      return _inner.send(request);
    }

    final sessionKey = CryptoUtils.randomBytes(32);
    try {
      final rsaKey = await _metadata.getRsaPublicKey();
      final wrappedKey = CryptoUtils.rsaOaepEncrypt(rsaKey, sessionKey);
      final encryptedBody =
          CryptoUtils.aesGcmEncrypt(sessionKey, request.bodyBytes);

      final encrypted = HttpInterceptorUtils.copyRequest(request)
        ..bodyBytes = utf8.encode(base64Encode(encryptedBody))
        ..headers[enabledHeader] = 'true'
        ..headers[sessionKeyHeader] = base64Encode(wrappedKey);

      final streamed = await _inner.send(encrypted);

      // Error responses are never encrypted (diagnostics must stay
      // readable); only 2xx bodies are proactively decrypted — even if
      // the response's ALE header was stripped in transit.
      if (streamed.statusCode < 200 || streamed.statusCode >= 300) {
        return streamed;
      }

      final response = await HttpInterceptorUtils.buffer(streamed);
      if (response.bodyBytes.isEmpty) {
        return HttpInterceptorUtils.toStreamed(response);
      }

      final plaintext = _decrypt(sessionKey, response.body);
      return HttpInterceptorUtils.toStreamed(
        http.Response.bytes(
          plaintext,
          response.statusCode,
          headers: Map.of(response.headers)..remove('content-length'),
          request: response.request,
          isRedirect: response.isRedirect,
          persistentConnection: response.persistentConnection,
          reasonPhrase: response.reasonPhrase,
        ),
      );
    } finally {
      // Zero-memory hygiene: the session key never outlives the exchange.
      CryptoUtils.zero(sessionKey);
    }
  }

  Uint8List _decrypt(Uint8List sessionKey, String body) {
    // String-first treatment: the server may deliver the Base64 payload as
    // a JSON-quoted string (docs/SECURITY.md §4.3).
    var payload = body.trim();
    if (payload.startsWith('"')) {
      payload = jsonDecode(payload) as String;
    }
    return CryptoUtils.aesGcmDecrypt(sessionKey, base64Decode(payload));
  }

  @override
  void close() => _inner.close();
}
