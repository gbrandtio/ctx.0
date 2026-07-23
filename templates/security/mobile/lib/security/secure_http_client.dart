import 'dart:convert';
import 'dart:math';
import 'dart:typed_data';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:http/http.dart' as http;
import 'package:pointycastle/export.dart';
import 'package:uuid/uuid.dart';

import 'crypto/ale_cipher.dart';
import 'crypto/p256.dart';
import 'secure_request.dart';

/// Raised when the API returns a non-success status for a secure request.
class CtxHttpException implements Exception {
  const CtxHttpException(this.statusCode, this.body);
  final int statusCode;
  final String body;
  @override
  String toString() => 'CtxHttpException($statusCode): $body';
}

/// The single HTTP client all API traffic flows through. It implements the ctx.0
/// wire protocol end to end: a per-install ECDSA device key (kept in secure
/// storage and enrolled with the API), ECDH+HKDF+AES-256-GCM ALE on every body,
/// and an ECDSA signature over each request. Extend it, but do not bypass it.
class SecureHttpClient {
  SecureHttpClient({
    required this.baseUrl,
    http.Client? httpClient,
    FlutterSecureStorage? storage,
  }) : _http = httpClient ?? http.Client(),
       _storage = storage ?? const FlutterSecureStorage();

  final String baseUrl;

  /// Language tag sent as `Accept-Language` on every secure request, so the API
  /// answers in the user's language. The `l10n` feature keeps it in step with
  /// the selected locale; left null, the API answers in its default language.
  String? acceptLanguage;

  final http.Client _http;
  final FlutterSecureStorage _storage;
  final SecureRandom _random = _seededRandom();

  static const String _scalarKey = 'ctx.device.scalar';
  static const String _deviceIdKey = 'ctx.device.id';

  Uint8List? _deviceScalar;
  Uint8List? _devicePublic;
  String? _deviceId;
  Uint8List? _serverAlePublic;

  /// Send [body] to [path] under the full wire protocol and return the decrypted
  /// JSON reply.
  Future<Map<String, dynamic>> secureSend(
    String method,
    String path,
    Map<String, dynamic> body,
  ) async {
    await _ensureDeviceEnrolled();
    final serverKey = await _ensureServerAleKey();

    final ephemeral = _generateKeyPair();
    final ephemeralPublic = P256.uncompressed(
      ephemeral.publicKey as ECPublicKey,
    );
    final iv = _randomBytes(12);
    final timestamp = DateTime.now().toUtc().millisecondsSinceEpoch.toString();

    final request = SecureRequestBuilder.build(
      method: method,
      pathAndQuery: path,
      plaintext: Uint8List.fromList(utf8.encode(jsonEncode(body))),
      serverAlePublic: serverKey,
      ephemeralPrivate: ephemeral.privateKey as ECPrivateKey,
      ephemeralPublic: ephemeralPublic,
      iv: iv,
      deviceScalar: _deviceScalar!,
      deviceId: _deviceId!,
      timestamp: timestamp,
    );

    final response = await _http.post(
      Uri.parse('$baseUrl$path'),
      headers: {
        ...request.headers,
        if (acceptLanguage != null) 'Accept-Language': acceptLanguage!,
      },
      body: request.body,
    );
    if (response.statusCode >= 400) {
      throw CtxHttpException(response.statusCode, response.body);
    }
    final envelope = AleEnvelope.fromJson(
      jsonDecode(response.body) as Map<String, dynamic>,
    );
    final plaintext = AleCipher.openResponse(envelope, request.responseKey);
    return jsonDecode(utf8.decode(plaintext)) as Map<String, dynamic>;
  }

  Future<void> _ensureDeviceEnrolled() async {
    if (_deviceScalar != null) return;

    final storedScalar = await _storage.read(key: _scalarKey);
    final storedId = await _storage.read(key: _deviceIdKey);
    if (storedScalar != null && storedId != null) {
      _deviceScalar = base64.decode(storedScalar);
      _devicePublic = P256.uncompressed(
        P256.publicKeyFromScalar(_deviceScalar!),
      );
      _deviceId = storedId;
    } else {
      final pair = _generateKeyPair();
      final scalar = P256.bigIntToBytes(
        (pair.privateKey as ECPrivateKey).d!,
        P256.fieldBytes,
      );
      _deviceScalar = scalar;
      _devicePublic = P256.uncompressed(pair.publicKey as ECPublicKey);
      _deviceId = const Uuid().v4();
      await _storage.write(key: _scalarKey, value: base64.encode(scalar));
      await _storage.write(key: _deviceIdKey, value: _deviceId);
    }

    final response = await _http.post(
      Uri.parse('$baseUrl/v1/security/devices'),
      headers: const {'Content-Type': 'application/json'},
      body: jsonEncode({
        'deviceId': _deviceId,
        'publicKey': base64.encode(_devicePublic!),
      }),
    );
    if (response.statusCode >= 400) {
      throw CtxHttpException(response.statusCode, response.body);
    }
  }

  Future<Uint8List> _ensureServerAleKey() async {
    if (_serverAlePublic != null) return _serverAlePublic!;
    final response = await _http.get(
      Uri.parse('$baseUrl/v1/security/ale-public-key'),
    );
    if (response.statusCode >= 400) {
      throw CtxHttpException(response.statusCode, response.body);
    }
    final json = jsonDecode(response.body) as Map<String, dynamic>;
    _serverAlePublic = base64.decode(json['publicKey'] as String);
    return _serverAlePublic!;
  }

  AsymmetricKeyPair<PublicKey, PrivateKey> _generateKeyPair() {
    final generator = ECKeyGenerator()
      ..init(
        ParametersWithRandom(ECKeyGeneratorParameters(P256.domain), _random),
      );
    return generator.generateKeyPair();
  }

  Uint8List _randomBytes(int n) {
    final bytes = Uint8List(n);
    for (var i = 0; i < n; i++) {
      bytes[i] = _random.nextUint8();
    }
    return bytes;
  }

  static SecureRandom _seededRandom() {
    final random = FortunaRandom();
    final seed = Uint8List(32);
    final entropy = Random.secure();
    for (var i = 0; i < seed.length; i++) {
      seed[i] = entropy.nextInt(256);
    }
    random.seed(KeyParameter(seed));
    return random;
  }
}
