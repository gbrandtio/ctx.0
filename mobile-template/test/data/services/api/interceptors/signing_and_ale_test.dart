import 'dart:convert';
import 'dart:typed_data';

import 'package:app_template/data/services/api/interceptors/ale_client.dart';
import 'package:app_template/data/services/api/interceptors/secure_device_signing_client.dart';
import 'package:app_template/data/services/api/security_metadata_service.dart';
import 'package:app_template/data/services/security/crypto_utils.dart';
import 'package:app_template/data/services/security/device_identity_service.dart';
import 'package:app_template/data/services/storage/secure_storage_service.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mocktail/mocktail.dart';
import 'package:pointycastle/asn1.dart';
import 'package:pointycastle/export.dart';

class _MockSecureStorage extends Mock implements SecureStorageService {}

Future<DeviceIdentityService> _freshIdentity() async {
  registerFallbackValue(Uint8List(0));
  final storage = _MockSecureStorage();
  when(() => storage.readDeviceId()).thenAnswer((_) async => null);
  when(() => storage.readDevicePrivateKey()).thenAnswer((_) async => null);
  when(() => storage.writeDeviceId(any())).thenAnswer((_) async {});
  when(() => storage.writeDevicePrivateKey(any())).thenAnswer((_) async {});
  final identity = DeviceIdentityService(storage);
  await identity.init();
  return identity;
}

bool _verify(
  String publicKeySpkiBase64,
  String canonical,
  String signatureBase64,
) {
  final spki = ASN1Parser(base64Decode(publicKeySpkiBase64)).nextObject()
      as ASN1Sequence;
  final point = Uint8List.fromList(
    (spki.elements![1] as ASN1BitString).stringValues!,
  );
  final params = ECDomainParameters('prime256v1');
  final publicKey = ECPublicKey(params.curve.decodePoint(point), params);

  final der =
      ASN1Parser(base64Decode(signatureBase64)).nextObject() as ASN1Sequence;
  final signature = ECSignature(
    (der.elements![0] as ASN1Integer).integer!,
    (der.elements![1] as ASN1Integer).integer!,
  );
  final verifier = Signer('SHA-256/ECDSA')
    ..init(false, PublicKeyParameter<ECPublicKey>(publicKey));
  return verifier.verifySignature(
    Uint8List.fromList(utf8.encode(canonical)),
    signature,
  );
}

void main() {
  group('SecureDeviceSigningClient', () {
    test('signs METHOD|PATH|TIMESTAMP|PLAINTEXT_BODY and sets both headers',
        () async {
      final identity = await _freshIdentity();
      late http.Request seen;
      final client = SecureDeviceSigningClient(
        MockClient((request) async {
          seen = request;
          return http.Response('{}', 200, request: request);
        }),
        identity,
      );

      const body = '{"email":"a@b.com"}';
      await client.post(
        Uri.parse('https://api.example.com/v1/Users/Login'),
        body: body,
      );

      expect(seen.headers[SecureDeviceSigningClient.deviceIdHeader],
          identity.deviceId);
      final header =
          seen.headers[SecureDeviceSigningClient.signatureHeader]!;
      final separator = header.indexOf(':');
      final timestamp = header.substring(0, separator);
      final signature = header.substring(separator + 1);
      // Canonical string: uppercase method, LOWERCASE path, plaintext body.
      final canonical = 'POST|/v1/users/login|$timestamp|$body';
      expect(
        _verify(identity.publicKeyBase64, canonical, signature),
        isTrue,
      );
    });

    test(
        'self-healing: 401 "Device not registered." registers and retries once',
        () async {
      final identity = await _freshIdentity();
      final calls = <http.Request>[];
      var registered = false;
      final client = SecureDeviceSigningClient(
        MockClient((request) async {
          calls.add(request);
          if (request.url.path.endsWith('/security/app-instances')) {
            registered = true;
            return http.Response('{}', 201, request: request);
          }
          if (!registered) {
            return http.Response(
              '{"status":401,"detail":"Device not registered."}',
              401,
              request: request,
            );
          }
          return http.Response('{"ok":true}', 200, request: request);
        }),
        identity,
      );

      final response = await client.post(
        Uri.parse('https://api.example.com/v1/orders'),
        body: '{"amount":500}',
      );

      expect(response.statusCode, 200);
      expect(calls, hasLength(3)); // original, registration, retry
      final registration =
          calls.singleWhere((r) => r.url.path.contains('app-instances'));
      final payload = jsonDecode(registration.body) as Map<String, dynamic>;
      expect(payload['deviceId'], identity.deviceId);
      expect(payload['publicKey'], identity.publicKeyBase64);
      expect(registration.headers[SecureDeviceSigningClient.deviceIdHeader],
          identity.deviceId);
    });
  });

  group('AleClient', () {
    late RSAPrivateKey rsaPrivate;
    late SecurityMetadataService metadata;

    setUpAll(() {
      final generator = RSAKeyGenerator()
        ..init(ParametersWithRandom(
          RSAKeyGeneratorParameters(BigInt.from(65537), 2048, 64),
          CryptoUtils.secureRandom(),
        ));
      final pair = generator.generateKeyPair();
      rsaPrivate = pair.privateKey;
      final publicKey = pair.publicKey;

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

      metadata = SecurityMetadataService(
        MockClient((request) async =>
            http.Response(jsonEncode({'alePublicKey': pem}), 200)),
      );
    });

    Uint8List unwrapSessionKey(String wrappedBase64) {
      final decryptor =
          OAEPEncoding.withCustomDigest(SHA256Digest.new, RSAEngine())
            ..init(false, PrivateKeyParameter<RSAPrivateKey>(rsaPrivate));
      return decryptor.process(base64Decode(wrappedBase64));
    }

    test('encrypts the request and decrypts the 2xx response', () async {
      const requestJson = '{"amount":500}';
      const responseJson = '{"id":"o_1"}';

      final client = AleClient(
        MockClient((request) async {
          expect(request.headers[AleClient.enabledHeader], 'true');
          final sessionKey = unwrapSessionKey(
              request.headers[AleClient.sessionKeyHeader]!);
          // Server decrypts the body…
          final plaintext = CryptoUtils.aesGcmDecrypt(
              sessionKey, base64Decode(request.body));
          expect(utf8.decode(plaintext), requestJson);
          // …and encrypts the response with the same session key.
          final encrypted = CryptoUtils.aesGcmEncrypt(
            sessionKey,
            Uint8List.fromList(utf8.encode(responseJson)),
          );
          return http.Response(base64Encode(encrypted), 200,
              request: request);
        }),
        metadata,
      );

      final response = await client.post(
        Uri.parse('https://api.example.com/v1/orders'),
        headers: {'Content-Type': 'application/json'},
        body: requestJson,
      );

      expect(response.body, responseJson);
    });

    test('error responses pass through unencrypted', () async {
      const problem = '{"status":409,"detail":"Conflict"}';
      final client = AleClient(
        MockClient(
            (request) async => http.Response(problem, 409, request: request)),
        metadata,
      );

      final response = await client.post(
        Uri.parse('https://api.example.com/v1/orders'),
        body: '{"amount":500}',
      );

      expect(response.statusCode, 409);
      expect(response.body, problem);
    });

    test('body-less requests are sent untouched', () async {
      late http.BaseRequest seen;
      final client = AleClient(
        MockClient((request) async {
          seen = request;
          return http.Response('{"plain":true}', 200, request: request);
        }),
        metadata,
      );

      final response =
          await client.get(Uri.parse('https://api.example.com/v1/items'));

      expect(seen.headers.containsKey(AleClient.enabledHeader), isFalse);
      expect(response.body, '{"plain":true}');
    });
  });
}
