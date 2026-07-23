import 'dart:convert';
import 'dart:typed_data';

import 'package:flutter_test/flutter_test.dart';

import 'package:ctxapp/security/crypto/ale_cipher.dart';
import 'package:ctxapp/security/crypto/p256.dart';
import 'package:ctxapp/security/crypto/request_signature.dart';

import 'vectors_loader.dart';

Uint8List b64(String s) => base64.decode(s);

void main() {
  final vectors = loadGoldenVectors();

  group('ALE (ECIES / AES-256-GCM) interop', () {
    final ale = vectors['ale'] as Map<String, dynamic>;

    test(
      'ECDH agreement matches from both sides and equals the golden key',
      () {
        final k1 = AleCipher.deriveKey(
          P256.privateKeyFromScalar(b64(ale['ephemeralPrivateB64'] as String)),
          b64(ale['serverPublicB64'] as String),
        );
        final k2 = AleCipher.deriveKey(
          P256.privateKeyFromScalar(b64(ale['serverPrivateB64'] as String)),
          b64(ale['ephemeralPublicB64'] as String),
        );
        expect(k1, equals(k2));
        expect(base64.encode(k1), equals(ale['derivedKeyB64']));
      },
    );

    test('encrypt reproduces the golden ciphertext + tag', () {
      final (ct, tag) = AleCipher.encrypt(
        b64(ale['derivedKeyB64'] as String),
        b64(ale['ivB64'] as String),
        Uint8List.fromList(utf8.encode(ale['plaintextUtf8'] as String)),
      );
      expect(base64.encode(ct), equals(ale['ciphertextB64']));
      expect(base64.encode(tag), equals(ale['tagB64']));
    });

    test('decrypt recovers the plaintext', () {
      final out = AleCipher.decrypt(
        b64(ale['derivedKeyB64'] as String),
        b64(ale['ivB64'] as String),
        b64(ale['ciphertextB64'] as String),
        b64(ale['tagB64'] as String),
      );
      expect(utf8.decode(out), equals(ale['plaintextUtf8']));
    });
  });

  group('Request signing (ECDSA P-256) interop', () {
    final s = vectors['signing'] as Map<String, dynamic>;
    final body = Uint8List.fromList(utf8.encode(s['bodyUtf8'] as String));

    test('canonical string matches', () {
      expect(
        RequestSignature.canonical(
          s['method'] as String,
          s['path'] as String,
          s['timestamp'] as String,
          body,
        ),
        equals(s['canonicalString']),
      );
    });

    test('verifies the golden signature from the API', () {
      expect(
        RequestSignature.verify(
          b64(s['devicePublicB64'] as String),
          s['signatureB64'] as String,
          s['method'] as String,
          s['path'] as String,
          s['timestamp'] as String,
          body,
        ),
        isTrue,
      );
    });
  });
}
