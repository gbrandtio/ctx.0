import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:pointycastle/export.dart';

import '../security/crypto_utils.dart';
import '../security/ctx_security_config.dart';

/// Bootstraps ALE by fetching the server's RSA-2048 public key from
/// GET /v1/security/metadata (docs/SECURITY.md §4.1). This handshake is
/// deliberately unencrypted — it is how encryption is established.
class SecurityMetadataService {
  SecurityMetadataService(this._client, this._config);

  final http.Client _client;
  final CtxSecurityConfig _config;

  RSAPublicKey? _cachedKey;

  /// The server's current ALE public key, fetched once and cached for the
  /// app session.
  Future<RSAPublicKey> getRsaPublicKey({bool forceRefresh = false}) async {
    final cached = _cachedKey;
    if (cached != null && !forceRefresh) return cached;

    final response = await _client
        .get(_config.resolveUri(_config.securityMetadataPath));
    assert(() {
      final remote = response.headers[ctxProtocolHeader.toLowerCase()];
      if (remote != null && remote != ctxProtocolVersion) {
        throw StateError(
          'Wire-protocol mismatch: ctx0_mobile_security speaks '
          '$ctxProtocolVersion but the API advertises $remote. Align the '
          'package versions (major.minor must match).',
        );
      }
      return true;
    }());
    if (response.statusCode != 200) {
      throw http.ClientException(
        'Security metadata fetch failed: ${response.statusCode}',
        response.request?.url,
      );
    }
    final json = jsonDecode(response.body) as Map<String, dynamic>;
    // Field name per the API's /v1/security/metadata contract
    // (APPLICATION_LAYER_SECURITY.md §3).
    final pem = json['alePublicKey'] as String;
    final key = CryptoUtils.parseRsaPublicKeyPem(pem);
    _cachedKey = key;
    return key;
  }
}
