import 'dart:convert';
import 'dart:math';
import 'dart:typed_data';

import 'package:pointycastle/asn1.dart';
import 'package:pointycastle/export.dart';

/// Shared low-level crypto helpers for request signing and ALE
/// (docs/SECURITY.md). Pure functions over pointycastle — no I/O.
abstract final class CryptoUtils {
  /// A CSPRNG seeded from the platform's secure entropy source.
  static SecureRandom secureRandom() {
    final seedSource = Random.secure();
    final seed =
        Uint8List.fromList(List.generate(32, (_) => seedSource.nextInt(256)));
    return FortunaRandom()..seed(KeyParameter(seed));
  }

  static Uint8List randomBytes(int length) =>
      secureRandom().nextBytes(length);

  /// Overwrites secret byte material with zeros ("Zero Memory" hygiene,
  /// docs/SECURITY.md §2).
  static void zero(Uint8List bytes) {
    for (var i = 0; i < bytes.length; i++) {
      bytes[i] = 0;
    }
  }

  // ---------------------------------------------------------------------
  // ECDSA P-256 (request signing)
  // ---------------------------------------------------------------------

  static ECDomainParameters get _p256 => ECDomainParameters('prime256v1');

  static AsymmetricKeyPair<ECPublicKey, ECPrivateKey> generateP256KeyPair() {
    final generator = ECKeyGenerator()
      ..init(ParametersWithRandom(
        ECKeyGeneratorParameters(_p256),
        secureRandom(),
      ));
    final pair = generator.generateKeyPair();
    return AsymmetricKeyPair(pair.publicKey, pair.privateKey);
  }

  /// Serializes the private scalar `d` as 32 big-endian bytes.
  static Uint8List encodePrivateScalar(ECPrivateKey key) =>
      _bigIntToFixedBytes(key.d!, 32);

  /// Rebuilds the P-256 key pair from a stored 32-byte private scalar.
  static AsymmetricKeyPair<ECPublicKey, ECPrivateKey> keyPairFromScalar(
    Uint8List scalar,
  ) {
    final params = _p256;
    final d = _bigIntFromBytes(scalar);
    final q = params.G * d;
    return AsymmetricKeyPair(ECPublicKey(q, params), ECPrivateKey(d, params));
  }

  /// SubjectPublicKeyInfo DER for the public key — the `publicKey` value
  /// sent to POST /v1/security/app-instances.
  static Uint8List encodePublicKeySpki(ECPublicKey key) {
    final algorithm = ASN1Sequence()
      ..add(ASN1ObjectIdentifier.fromIdentifierString('1.2.840.10045.2.1'))
      ..add(ASN1ObjectIdentifier.fromIdentifierString('1.2.840.10045.3.1.7'));
    final point = key.Q!.getEncoded(false); // uncompressed 0x04 || X || Y
    final spki = ASN1Sequence()
      ..add(algorithm)
      ..add(ASN1BitString(stringValues: point));
    return spki.encode();
  }

  /// ECDSA P-256 / SHA-256 signature, ASN.1 DER encoded
  /// (docs/HTTP_HANDLING.md "Request Signing").
  static Uint8List signP256(ECPrivateKey key, Uint8List payload) {
    final signer = Signer('SHA-256/ECDSA')
      ..init(
        true,
        ParametersWithRandom(
          PrivateKeyParameter<ECPrivateKey>(key),
          secureRandom(),
        ),
      );
    final signature = signer.generateSignature(payload) as ECSignature;
    final der = ASN1Sequence()
      ..add(ASN1Integer(signature.r))
      ..add(ASN1Integer(signature.s));
    return der.encode();
  }

  // ---------------------------------------------------------------------
  // RSA-OAEP (SHA-256) — ALE session-key wrapping
  // ---------------------------------------------------------------------

  /// Parses a PEM-encoded (or bare Base64 DER) SubjectPublicKeyInfo
  /// RSA-2048 public key as served by GET /v1/security/metadata.
  static RSAPublicKey parseRsaPublicKeyPem(String pem) {
    final base64Body = pem
        .replaceAll(RegExp(r'-----(BEGIN|END)[A-Z ]+-----'), '')
        .replaceAll(RegExp(r'\s'), '');
    final der = Uint8List.fromList(base64.decode(base64Body));

    final spki = ASN1Parser(der).nextObject() as ASN1Sequence;
    final keyBits = spki.elements![1] as ASN1BitString;
    final keySeq =
        ASN1Parser(Uint8List.fromList(keyBits.stringValues!)).nextObject()
            as ASN1Sequence;
    final modulus = (keySeq.elements![0] as ASN1Integer).integer!;
    final exponent = (keySeq.elements![1] as ASN1Integer).integer!;
    return RSAPublicKey(modulus, exponent);
  }

  /// RSA-OAEP with SHA-256 (RSA/ECB/OAEPWithSHA-256AndMGF1Padding).
  static Uint8List rsaOaepEncrypt(RSAPublicKey key, Uint8List data) {
    final engine = OAEPEncoding.withCustomDigest(SHA256Digest.new, RSAEngine())
      ..init(true, PublicKeyParameter<RSAPublicKey>(key));
    return engine.process(data);
  }

  // ---------------------------------------------------------------------
  // AES-256-GCM — ALE payload encryption
  // ---------------------------------------------------------------------

  /// Encrypts [plaintext]; returns `Nonce (12) | Tag (16) | Ciphertext (n)`
  /// per the ALE payload format (docs/HTTP_HANDLING.md).
  static Uint8List aesGcmEncrypt(Uint8List key, Uint8List plaintext) {
    final nonce = randomBytes(12);
    final cipher = GCMBlockCipher(AESEngine())
      ..init(true, AEADParameters(KeyParameter(key), 128, nonce, Uint8List(0)));
    final output = cipher.process(plaintext); // ciphertext || tag(16)
    final ciphertext = output.sublist(0, output.length - 16);
    final tag = output.sublist(output.length - 16);
    return Uint8List.fromList([...nonce, ...tag, ...ciphertext]);
  }

  /// Decrypts a `Nonce (12) | Tag (16) | Ciphertext (n)` payload.
  static Uint8List aesGcmDecrypt(Uint8List key, Uint8List payload) {
    if (payload.length < 28) {
      throw ArgumentError('ALE payload too short: ${payload.length} bytes');
    }
    final nonce = payload.sublist(0, 12);
    final tag = payload.sublist(12, 28);
    final ciphertext = payload.sublist(28);
    final cipher = GCMBlockCipher(AESEngine())
      ..init(
        false,
        AEADParameters(KeyParameter(key), 128, nonce, Uint8List(0)),
      );
    // pointycastle expects ciphertext || tag on decrypt.
    return cipher.process(Uint8List.fromList([...ciphertext, ...tag]));
  }

  // ---------------------------------------------------------------------

  static Uint8List _bigIntToFixedBytes(BigInt value, int length) {
    final result = Uint8List(length);
    var v = value;
    for (var i = length - 1; i >= 0; i--) {
      result[i] = (v & BigInt.from(0xff)).toInt();
      v = v >> 8;
    }
    return result;
  }

  static BigInt _bigIntFromBytes(Uint8List bytes) {
    var result = BigInt.zero;
    for (final byte in bytes) {
      result = (result << 8) | BigInt.from(byte);
    }
    return result;
  }
}
