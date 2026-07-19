import 'dart:convert';
import 'dart:typed_data';

import 'package:pointycastle/export.dart';

import 'p256.dart';

/// ECDSA P-256 request signing for ctx.0, matching the API's `RequestSignature`.
/// Signatures use the IEEE P1363 fixed-width (r||s) encoding, base64.
class RequestSignature {
  const RequestSignature._();

  /// Build the canonical string that is signed and verified.
  static String canonical(String method, String pathAndQuery, String timestamp, Uint8List body) {
    final bodyHash = _hex(SHA256Digest().process(body));
    return [method.toUpperCase(), pathAndQuery, timestamp, bodyHash].join('\n');
  }

  /// Sign the canonical string with a device private scalar. Returns base64(P1363).
  ///
  /// Uses deterministic ECDSA (RFC 6979) so signing needs no entropy source and
  /// is reproducible; the API verifier accepts it like any valid signature.
  static String sign(Uint8List devicePrivateScalar, String method, String pathAndQuery, String timestamp, Uint8List body) {
    final priv = P256.privateKeyFromScalar(devicePrivateScalar);
    final signer = ECDSASigner(SHA256Digest(), HMac(SHA256Digest(), 64));
    signer.init(true, PrivateKeyParameter<ECPrivateKey>(priv));
    final sig = signer.generateSignature(
      Uint8List.fromList(utf8.encode(canonical(method, pathAndQuery, timestamp, body))),
    ) as ECSignature;
    final r = P256.bigIntToBytes(sig.r, P256.fieldBytes);
    final s = P256.bigIntToBytes(_normalizeS(sig.s), P256.fieldBytes);
    return base64.encode(Uint8List.fromList([...r, ...s]));
  }

  /// Verify a base64(P1363) signature against a device uncompressed public key.
  static bool verify(
    Uint8List deviceUncompressedPublic,
    String signatureB64,
    String method,
    String pathAndQuery,
    String timestamp,
    Uint8List body,
  ) {
    final Uint8List raw;
    try {
      raw = base64.decode(signatureB64);
    } on FormatException {
      return false;
    }
    if (raw.length != 64) return false;
    final r = _bytesToBigInt(raw.sublist(0, 32));
    final s = _bytesToBigInt(raw.sublist(32));

    final pub = P256.publicKeyFromUncompressed(deviceUncompressedPublic);
    final verifier = ECDSASigner(SHA256Digest());
    verifier.init(false, PublicKeyParameter<ECPublicKey>(pub));
    return verifier.verifySignature(
      Uint8List.fromList(utf8.encode(canonical(method, pathAndQuery, timestamp, body))),
      ECSignature(r, s),
    );
  }

  // Low-S normalization keeps signatures in the canonical half-order form that
  // .NET's verifier accepts regardless of which representation pointycastle picks.
  static BigInt _normalizeS(BigInt s) {
    final n = P256.domain.n;
    final halfN = n >> 1;
    return s.compareTo(halfN) > 0 ? n - s : s;
  }

  static BigInt _bytesToBigInt(Uint8List bytes) {
    var result = BigInt.zero;
    for (final b in bytes) {
      result = (result << 8) | BigInt.from(b);
    }
    return result;
  }

  static String _hex(Uint8List bytes) {
    final sb = StringBuffer();
    for (final b in bytes) {
      sb.write(b.toRadixString(16).padLeft(2, '0'));
    }
    return sb.toString();
  }
}
