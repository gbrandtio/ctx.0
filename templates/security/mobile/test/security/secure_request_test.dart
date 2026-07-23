import 'dart:convert';
import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';

import 'package:ctxapp/security/crypto/ale_cipher.dart';
import 'package:ctxapp/security/crypto/ctx_protocol.dart';
import 'package:ctxapp/security/crypto/p256.dart';
import 'package:ctxapp/security/crypto/request_signature.dart';
import 'package:ctxapp/security/secure_request.dart';

import 'vectors_loader.dart';

Uint8List b64(String s) => base64.decode(s);

void main() {
  final vectors = loadGoldenVectors();
  final ale = vectors['ale'] as Map<String, dynamic>;
  final sign = vectors['signing'] as Map<String, dynamic>;

  test('a built secure request verifies and decrypts as the server would', () {
    final plaintext = Uint8List.fromList(utf8.encode('{"message":"hi"}'));
    const method = 'POST';
    const path = '/v1/ping';
    const timestamp = '1730000000000';

    final request = SecureRequestBuilder.build(
      method: method,
      pathAndQuery: path,
      plaintext: plaintext,
      serverAlePublic: b64(ale['serverPublicB64'] as String),
      ephemeralPrivate: P256.privateKeyFromScalar(
        b64(ale['ephemeralPrivateB64'] as String),
      ),
      ephemeralPublic: b64(ale['ephemeralPublicB64'] as String),
      iv: b64(ale['ivB64'] as String),
      deviceScalar: b64(sign['devicePrivateB64'] as String),
      deviceId: 'device-1',
      timestamp: timestamp,
    );

    // Headers carry the protocol contract.
    expect(
      request.headers[CtxProtocol.protocolHeader],
      equals(CtxProtocol.version),
    );
    expect(request.headers[CtxProtocol.deviceIdHeader], equals('device-1'));

    // Server side: verify the signature over the exact body bytes.
    final signatureOk = RequestSignature.verify(
      b64(sign['devicePublicB64'] as String),
      request.headers[CtxProtocol.signatureHeader]!,
      method,
      path,
      timestamp,
      request.body,
    );
    expect(signatureOk, isTrue);

    // Server side: derive the key from its static private + the request's epk,
    // then decrypt the envelope back to the original plaintext.
    final envelope = AleEnvelope.fromJson(
      jsonDecode(utf8.decode(request.body)) as Map<String, dynamic>,
    );
    final serverKey = AleCipher.deriveKey(
      P256.privateKeyFromScalar(b64(ale['serverPrivateB64'] as String)),
      base64.decode(envelope.epk!),
    );
    expect(serverKey, equals(request.responseKey));

    final recovered = AleCipher.decrypt(
      serverKey,
      base64.decode(envelope.iv),
      base64.decode(envelope.ct),
      base64.decode(envelope.tag),
    );
    expect(utf8.decode(recovered), equals('{"message":"hi"}'));
  });
}
