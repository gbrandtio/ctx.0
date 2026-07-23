import 'dart:convert';
import 'dart:typed_data';

import 'package:pointycastle/export.dart';

import 'ctx_protocol.dart';
import 'p256.dart';

/// One ALE-protected payload. A request envelope carries the client's ephemeral
/// public key ([epk]); a response envelope omits it (the key is already derived).
class AleEnvelope {
  const AleEnvelope({
    this.epk,
    required this.iv,
    required this.ct,
    required this.tag,
  });

  final String? epk;
  final String iv;
  final String ct;
  final String tag;

  factory AleEnvelope.fromJson(Map<String, dynamic> json) => AleEnvelope(
    epk: json['Epk'] as String?,
    iv: json['Iv'] as String,
    ct: json['Ct'] as String,
    tag: json['Tag'] as String,
  );

  /// Serialized with the same field names/order the API uses, so the signed
  /// body bytes are produced consistently.
  Map<String, dynamic> toJson() => <String, dynamic>{
    'Epk': epk,
    'Iv': iv,
    'Ct': ct,
    'Tag': tag,
  };
}

/// Application-Layer Encryption for ctx.0: ECIES over NIST P-256 with
/// AES-256-GCM, matching the API's `AleCipher`.
class AleCipher {
  const AleCipher._();

  static const int _tagBytes = 16;

  /// ECDH + HKDF-SHA256 -> 32-byte AES key. Order of the two keys does not matter.
  static Uint8List deriveKey(
    ECPrivateKey ownPrivate,
    Uint8List otherUncompressedPublic,
  ) {
    final other = P256.publicKeyFromUncompressed(otherUncompressedPublic);
    final agreement = ECDHBasicAgreement()..init(ownPrivate);
    final sharedX = agreement.calculateAgreement(other);
    final ikm = P256.bigIntToBytes(sharedX, P256.fieldBytes);

    final hkdf = HKDFKeyDerivator(SHA256Digest());
    hkdf.init(
      HkdfParameters(
        ikm,
        32,
        Uint8List(32), // 32-byte zero salt, matching the API
        utf8.encode(CtxProtocol.aleHkdfInfo),
      ),
    );
    final out = Uint8List(32);
    hkdf.deriveKey(null, 0, out, 0);
    return out;
  }

  static (Uint8List ct, Uint8List tag) encrypt(
    Uint8List key,
    Uint8List iv,
    Uint8List plaintext,
  ) {
    final gcm = GCMBlockCipher(AESEngine())
      ..init(
        true,
        AEADParameters(KeyParameter(key), _tagBytes * 8, iv, Uint8List(0)),
      );
    final out = gcm.process(plaintext);
    final ct = out.sublist(0, out.length - _tagBytes);
    final tag = out.sublist(out.length - _tagBytes);
    return (ct, tag);
  }

  static Uint8List decrypt(
    Uint8List key,
    Uint8List iv,
    Uint8List ct,
    Uint8List tag,
  ) {
    final gcm = GCMBlockCipher(AESEngine())
      ..init(
        false,
        AEADParameters(KeyParameter(key), _tagBytes * 8, iv, Uint8List(0)),
      );
    final input = Uint8List(ct.length + tag.length)
      ..setRange(0, ct.length, ct)
      ..setRange(ct.length, ct.length + tag.length, tag);
    return gcm.process(input);
  }

  /// Seal [plaintext] to the recipient's static public key, producing a request
  /// envelope. Returns the envelope and the derived key (reused to open the
  /// response).
  static (AleEnvelope envelope, Uint8List key) sealRequest(
    Uint8List plaintext,
    Uint8List recipientUncompressedPublic,
    ECPrivateKey ephemeralPrivate,
    Uint8List ephemeralUncompressedPublic,
    Uint8List iv,
  ) {
    final key = deriveKey(ephemeralPrivate, recipientUncompressedPublic);
    final (ct, tag) = encrypt(key, iv, plaintext);
    final envelope = AleEnvelope(
      epk: base64.encode(ephemeralUncompressedPublic),
      iv: base64.encode(iv),
      ct: base64.encode(ct),
      tag: base64.encode(tag),
    );
    return (envelope, key);
  }

  /// Open a response envelope with the key derived when sealing the request.
  static Uint8List openResponse(AleEnvelope envelope, Uint8List key) {
    return decrypt(
      key,
      base64.decode(envelope.iv),
      base64.decode(envelope.ct),
      base64.decode(envelope.tag),
    );
  }
}
