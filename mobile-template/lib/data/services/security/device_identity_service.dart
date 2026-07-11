import 'dart:convert';
import 'dart:typed_data';

import 'package:pointycastle/export.dart';
import 'package:uuid/uuid.dart';

import '../storage/secure_storage_service.dart';
import 'crypto_utils.dart';

/// Per-device identity for request signing (docs/SECURITY.md §1, §4.1):
/// a UUID v4 device id plus an ECDSA P-256 key pair generated on first run
/// and kept in platform secure storage (Keychain / Keystore-encrypted).
/// There is no shared signing secret anywhere in the app.
class DeviceIdentityService {
  DeviceIdentityService(this._secureStorage);

  final SecureStorageService _secureStorage;

  String? _deviceId;
  AsymmetricKeyPair<ECPublicKey, ECPrivateKey>? _keyPair;

  /// Loads (or generates) the device id and key pair. Called once at
  /// bootstrap, before the first signed request.
  Future<void> init() async {
    var deviceId = await _secureStorage.readDeviceId();
    if (deviceId == null) {
      deviceId = const Uuid().v4();
      await _secureStorage.writeDeviceId(deviceId);
    }
    _deviceId = deviceId;

    final storedScalar = await _secureStorage.readDevicePrivateKey();
    if (storedScalar != null) {
      _keyPair = CryptoUtils.keyPairFromScalar(storedScalar);
      CryptoUtils.zero(storedScalar);
    } else {
      final pair = CryptoUtils.generateP256KeyPair();
      final scalar = CryptoUtils.encodePrivateScalar(pair.privateKey);
      await _secureStorage.writeDevicePrivateKey(scalar);
      CryptoUtils.zero(scalar);
      _keyPair = pair;
    }
  }

  String get deviceId {
    final id = _deviceId;
    if (id == null) throw StateError('DeviceIdentityService not initialized');
    return id;
  }

  /// Base64 SubjectPublicKeyInfo — the `publicKey` sent when registering
  /// the app instance (docs/HTTP_HANDLING.md "Self-Healing Registration").
  String get publicKeyBase64 {
    final pair = _requireKeyPair;
    return base64Encode(CryptoUtils.encodePublicKeySpki(pair.publicKey));
  }

  /// Signs `METHOD|PATH|TIMESTAMP|BODY` (plaintext body — see
  /// docs/SECURITY.md §4.2) and returns the Base64 DER signature.
  String sign(String canonicalPayload) {
    final pair = _requireKeyPair;
    final signature = CryptoUtils.signP256(
      pair.privateKey,
      Uint8List.fromList(utf8.encode(canonicalPayload)),
    );
    return base64Encode(signature);
  }

  AsymmetricKeyPair<ECPublicKey, ECPrivateKey> get _requireKeyPair {
    final pair = _keyPair;
    if (pair == null) {
      throw StateError('DeviceIdentityService not initialized');
    }
    return pair;
  }
}
