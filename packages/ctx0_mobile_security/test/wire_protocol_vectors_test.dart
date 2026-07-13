import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:ctx0_mobile_security/ctx0_mobile_security.dart';
import 'package:flutter_test/flutter_test.dart';

/// Golden wire-protocol vectors shared with the API's Ctx0.Security test
/// suite (packages/protocol/wire_protocol_vectors.json). If this test
/// fails after a change, the mobile and server security planes no longer
/// speak the same protocol — bump CtxProtocol on BOTH sides and
/// regenerate the vectors deliberately.
void main() {
  final vectors = jsonDecode(
    File('../protocol/wire_protocol_vectors.json').readAsStringSync(),
  ) as Map<String, dynamic>;

  test('package protocol version matches the shared vectors', () {
    expect(ctxProtocolVersion, vectors['protocolVersion']);
  });

  test('canonical signing string: METHOD|lowercase path|timestamp|body', () {
    final signing = vectors['signing'] as Map<String, dynamic>;
    final canonical = '${(signing['method'] as String).toUpperCase()}'
        '|${(signing['path'] as String).toLowerCase()}'
        '|${signing['timestamp']}'
        '|${signing['body']}';
    expect(canonical, signing['canonical']);
  });

  test('AES-256-GCM Nonce|Tag|Ciphertext payload decrypts like the server',
      () {
    final aes = vectors['aesGcm'] as Map<String, dynamic>;
    final key = Uint8List.fromList(base64Decode(aes['keyBase64'] as String));
    final payload =
        Uint8List.fromList(base64Decode(aes['payloadBase64'] as String));
    final plaintext = CryptoUtils.aesGcmDecrypt(key, payload);
    expect(utf8.decode(plaintext), aes['plaintext']);
  });
}
