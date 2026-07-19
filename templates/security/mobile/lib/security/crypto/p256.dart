import 'dart:typed_data';

import 'package:pointycastle/export.dart';

/// NIST P-256 (secp256r1) key helpers using the same byte representations as the
/// API: a private key is a raw 32-byte big-endian scalar; a public key is an
/// uncompressed point (0x04 || X[32] || Y[32], 65 bytes).
class P256 {
  const P256._();

  static const int fieldBytes = 32;

  static final ECDomainParameters domain = ECDomainParameters('prime256v1');

  static ECPrivateKey privateKeyFromScalar(Uint8List d) {
    return ECPrivateKey(_bytesToBigInt(d), domain);
  }

  /// Derive the public key for a private scalar (Q = d·G).
  static ECPublicKey publicKeyFromScalar(Uint8List d) {
    final q = domain.G * _bytesToBigInt(d);
    return ECPublicKey(q, domain);
  }

  static ECPublicKey publicKeyFromUncompressed(Uint8List bytes) {
    if (bytes.length != 65 || bytes[0] != 0x04) {
      throw ArgumentError('Expected a 65-byte uncompressed P-256 point.');
    }
    final q = domain.curve.decodePoint(bytes);
    if (q == null) {
      throw ArgumentError('Invalid P-256 point.');
    }
    return ECPublicKey(q, domain);
  }

  static Uint8List uncompressed(ECPublicKey key) {
    return key.Q!.getEncoded(false);
  }

  /// Encode a BigInt as a fixed-width big-endian byte array.
  static Uint8List bigIntToBytes(BigInt value, int length) {
    final result = Uint8List(length);
    var v = value;
    for (var i = length - 1; i >= 0; i--) {
      result[i] = (v & BigInt.from(0xff)).toInt();
      v = v >> 8;
    }
    return result;
  }

  static BigInt _bytesToBigInt(Uint8List bytes) {
    var result = BigInt.zero;
    for (final b in bytes) {
      result = (result << 8) | BigInt.from(b);
    }
    return result;
  }
}
