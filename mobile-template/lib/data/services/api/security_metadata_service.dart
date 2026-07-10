import 'dart:convert';

import 'package:http/http.dart' as http;
import 'package:pointycastle/export.dart';

import '../../../core/constants/api_constants.dart';
import '../security/crypto_utils.dart';

/// Bootstraps ALE by fetching the server's RSA-2048 public key from
/// GET /v1/security/metadata (docs/SECURITY.md §4.1). This handshake is
/// deliberately unencrypted — it is how encryption is established.
class SecurityMetadataService {
  SecurityMetadataService(this._client);

  final http.Client _client;

  RSAPublicKey? _cachedKey;

  /// The server's current ALE public key, fetched once and cached for the
  /// app session.
  Future<RSAPublicKey> getRsaPublicKey({bool forceRefresh = false}) async {
    final cached = _cachedKey;
    if (cached != null && !forceRefresh) return cached;

    final response =
        await _client.get(ApiConstants.uri(ApiConstants.securityMetadata));
    if (response.statusCode != 200) {
      throw http.ClientException(
        'Security metadata fetch failed: ${response.statusCode}',
        response.request?.url,
      );
    }
    final json = jsonDecode(response.body) as Map<String, dynamic>;
    final pem = json['publicKey'] as String;
    final key = CryptoUtils.parseRsaPublicKeyPem(pem);
    _cachedKey = key;
    return key;
  }
}
