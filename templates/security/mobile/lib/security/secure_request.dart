import 'dart:convert';
import 'dart:typed_data';

import 'package:pointycastle/export.dart';

import 'crypto/ale_cipher.dart';
import 'crypto/ctx_protocol.dart';
import 'crypto/request_signature.dart';

/// A fully-formed secure request: the headers and exact body bytes to send, plus
/// the ALE key needed to open the response.
class SecureRequest {
  const SecureRequest({
    required this.headers,
    required this.body,
    required this.responseKey,
  });

  final Map<String, String> headers;
  final Uint8List body;
  final Uint8List responseKey;
}

/// Assembles a ctx.0 secure request from its inputs. Pure and deterministic given
/// its arguments, so it is fully unit-testable without platform plugins: it seals
/// the body with ALE, signs the canonical request, and returns the ready headers.
class SecureRequestBuilder {
  const SecureRequestBuilder._();

  static SecureRequest build({
    required String method,
    required String pathAndQuery,
    required Uint8List plaintext,
    required Uint8List serverAlePublic,
    required ECPrivateKey ephemeralPrivate,
    required Uint8List ephemeralPublic,
    required Uint8List iv,
    required Uint8List deviceScalar,
    required String deviceId,
    required String timestamp,
  }) {
    final (envelope, key) = AleCipher.sealRequest(
      plaintext,
      serverAlePublic,
      ephemeralPrivate,
      ephemeralPublic,
      iv,
    );
    final body = Uint8List.fromList(utf8.encode(jsonEncode(envelope.toJson())));
    final signature = RequestSignature.sign(
      deviceScalar,
      method,
      pathAndQuery,
      timestamp,
      body,
    );

    return SecureRequest(
      headers: <String, String>{
        'Content-Type': 'application/json',
        CtxProtocol.protocolHeader: CtxProtocol.version,
        CtxProtocol.deviceIdHeader: deviceId,
        CtxProtocol.timestampHeader: timestamp,
        CtxProtocol.signatureHeader: signature,
      },
      body: body,
      responseKey: key,
    );
  }
}
