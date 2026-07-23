import 'dart:convert';

import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// A consent decision as held on the device.
class ConsentDecision {
  const ConsentDecision({
    required this.policyVersion,
    required this.purposes,
    required this.decidedAt,
    this.synced = false,
  });

  /// The privacy-notice version the user was shown when they decided.
  final String policyVersion;

  /// Optional purposes the user accepted; empty means essential processing only.
  final Set<String> purposes;

  final DateTime decidedAt;

  /// Whether the server has this decision. A decision made before sign-in is
  /// stored locally and pushed once a session exists.
  final bool synced;

  bool accepted(String purpose) => purposes.contains(purpose);

  ConsentDecision copyWith({bool? synced}) => ConsentDecision(
    policyVersion: policyVersion,
    purposes: purposes,
    decidedAt: decidedAt,
    synced: synced ?? this.synced,
  );

  Map<String, dynamic> toJson() => {
    'policyVersion': policyVersion,
    'purposes': purposes.toList()..sort(),
    'decidedAt': decidedAt.toIso8601String(),
    'synced': synced,
  };

  factory ConsentDecision.fromJson(Map<String, dynamic> json) =>
      ConsentDecision(
        policyVersion: json['policyVersion'] as String,
        purposes: ((json['purposes'] as List<dynamic>?) ?? const [])
            .cast<String>()
            .toSet(),
        decidedAt: DateTime.parse(json['decidedAt'] as String),
        synced: (json['synced'] as bool?) ?? false,
      );
}

/// Persists the user's consent decision on the device.
abstract class ConsentStore {
  Future<ConsentDecision?> read();
  Future<void> write(ConsentDecision decision);
  Future<void> clear();
}

/// [ConsentStore] backed by platform secure storage. Consent is recorded locally
/// first so the banner can be answered before there is an account to attach it
/// to — the decision is pushed to the server as soon as a session exists.
class SecureConsentStore implements ConsentStore {
  SecureConsentStore([FlutterSecureStorage? storage])
    : _storage = storage ?? const FlutterSecureStorage();

  final FlutterSecureStorage _storage;
  static const String _key = 'ctx.gdpr.consent';

  @override
  Future<ConsentDecision?> read() async {
    final raw = await _storage.read(key: _key);
    if (raw == null) return null;
    return ConsentDecision.fromJson(jsonDecode(raw) as Map<String, dynamic>);
  }

  @override
  Future<void> write(ConsentDecision decision) =>
      _storage.write(key: _key, value: jsonEncode(decision.toJson()));

  @override
  Future<void> clear() => _storage.delete(key: _key);
}
