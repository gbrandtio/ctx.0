import 'dart:convert';
import 'dart:typed_data';

import 'package:ctx0_mobile_security/ctx0_mobile_security.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:pointycastle/asn1.dart';
import 'package:pointycastle/export.dart';

void main() {
  group('AES-256-GCM (ALE payload)', () {
    test('round-trips and uses the Nonce|Tag|Ciphertext layout', () {
      final key = CryptoUtils.randomBytes(32);
      final plaintext = Uint8List.fromList(utf8.encode('{"amount":500}'));

      final payload = CryptoUtils.aesGcmEncrypt(key, plaintext);

      // 12-byte nonce + 16-byte tag + ciphertext of plaintext length.
      expect(payload.length, 12 + 16 + plaintext.length);
      expect(CryptoUtils.aesGcmDecrypt(key, payload), plaintext);
    });

    test('tampered ciphertext fails authentication', () {
      final key = CryptoUtils.randomBytes(32);
      final payload = CryptoUtils.aesGcmEncrypt(
        key,
        Uint8List.fromList(utf8.encode('{"amount":500}')),
      );
      payload[payload.length - 1] ^= 0xff;

      expect(
        () => CryptoUtils.aesGcmDecrypt(key, payload),
        throwsA(anything),
      );
    });
  });

  group('ECDSA P-256 (request signing)', () {
    test('signature over the canonical payload verifies with the public key',
        () {
      final pair = CryptoUtils.generateP256KeyPair();
      final payload = Uint8List.fromList(
        utf8.encode('POST|/v1/users/login|1700000000|{"email":"a@b.com"}'),
      );

      final der = CryptoUtils.signP256(pair.privateKey, payload);

      final seq =
          ASN1Parser(der).nextObject() as ASN1Sequence;
      final signature = ECSignature(
        (seq.elements![0] as ASN1Integer).integer!,
        (seq.elements![1] as ASN1Integer).integer!,
      );
      final verifier = Signer('SHA-256/ECDSA')
        ..init(false, PublicKeyParameter<ECPublicKey>(pair.publicKey));
      expect(verifier.verifySignature(payload, signature), isTrue);
    });

    test('key pair survives the stored-scalar round trip', () {
      final pair = CryptoUtils.generateP256KeyPair();
      final scalar = CryptoUtils.encodePrivateScalar(pair.privateKey);

      final restored = CryptoUtils.keyPairFromScalar(scalar);

      expect(restored.privateKey.d, pair.privateKey.d);
      expect(restored.publicKey.Q, pair.publicKey.Q);
    });

    test('SPKI export is valid DER with the uncompressed point', () {
      final pair = CryptoUtils.generateP256KeyPair();

      final spki = CryptoUtils.encodePublicKeySpki(pair.publicKey);

      final seq = ASN1Parser(spki).nextObject() as ASN1Sequence;
      final bits = seq.elements![1] as ASN1BitString;
      expect(bits.stringValues!.first, 0x04); // uncompressed point marker
      expect(bits.stringValues!.length, 65); // 1 + 32 (X) + 32 (Y)
    });
  });

  group('RSA-OAEP SHA-256 (session-key wrapping)', () {
    late AsymmetricKeyPair<PublicKey, PrivateKey> rsaPair;

    setUpAll(() {
      final generator = RSAKeyGenerator()
        ..init(ParametersWithRandom(
          RSAKeyGeneratorParameters(BigInt.from(65537), 2048, 64),
          CryptoUtils.secureRandom(),
        ));
      rsaPair = generator.generateKeyPair();
    });

    test('wrapped key unwraps with the private key', () {
      final sessionKey = CryptoUtils.randomBytes(32);

      final wrapped = CryptoUtils.rsaOaepEncrypt(
        rsaPair.publicKey as RSAPublicKey,
        sessionKey,
      );

      final decryptor =
          OAEPEncoding.withCustomDigest(SHA256Digest.new, RSAEngine())
            ..init(
              false,
              PrivateKeyParameter<RSAPrivateKey>(
                rsaPair.privateKey as RSAPrivateKey,
              ),
            );
      expect(decryptor.process(wrapped), sessionKey);
    });

    test('parseRsaPublicKeyPem reads a PEM SubjectPublicKeyInfo', () {
      final publicKey = rsaPair.publicKey as RSAPublicKey;
      final keySeq = ASN1Sequence()
        ..add(ASN1Integer(publicKey.modulus))
        ..add(ASN1Integer(publicKey.exponent));
      final spki = ASN1Sequence()
        ..add(ASN1Sequence()
          ..add(ASN1ObjectIdentifier.fromIdentifierString(
              '1.2.840.113549.1.1.1'))
          ..add(ASN1Null()))
        ..add(ASN1BitString(stringValues: keySeq.encode()));
      final pem = '-----BEGIN PUBLIC KEY-----\n'
          '${base64Encode(spki.encode())}\n'
          '-----END PUBLIC KEY-----';

      final parsed = CryptoUtils.parseRsaPublicKeyPem(pem);

      expect(parsed.modulus, publicKey.modulus);
      expect(parsed.exponent, publicKey.exponent);
    });
  });
}
