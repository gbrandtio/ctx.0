import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../data/consent_store.dart';
import '../data/privacy_repository.dart';

/// The optional processing purposes the banner asks about. Essential processing
/// (running the account itself) is not consent-based and is never listed here.
const Set<String> ctxOptionalPurposes = {'analytics', 'marketing'};

/// Whether the banner should be on screen, and what the user last decided.
final class ConsentState extends Equatable {
  const ConsentState({this.decision, this.policyVersion, this.prompting = false, this.error});

  final ConsentDecision? decision;

  /// The notice version in force. Local until the server has been reached.
  final String? policyVersion;

  /// True while the user still owes a decision for the current notice version.
  final bool prompting;

  final String? error;

  ConsentState copyWith({
    ConsentDecision? decision,
    String? policyVersion,
    bool? prompting,
    String? error,
  }) =>
      ConsentState(
        decision: decision ?? this.decision,
        policyVersion: policyVersion ?? this.policyVersion,
        prompting: prompting ?? this.prompting,
        error: error,
      );

  /// Whether the user has accepted a given optional purpose.
  bool accepted(String purpose) => decision?.accepted(purpose) ?? false;

  @override
  List<Object?> get props => [decision, policyVersion, prompting, error];
}

/// Drives the consent banner. The decision is written to the device first so the
/// banner works before sign-in, then pushed to the server's audit trail as soon
/// as a session exists — and re-prompted whenever the notice version changes.
class ConsentCubit extends Cubit<ConsentState> {
  ConsentCubit(this._store, this._repository, {String fallbackPolicyVersion = '1'})
      : _fallbackPolicyVersion = fallbackPolicyVersion,
        super(const ConsentState());

  final ConsentStore _store;
  final PrivacyRepository _repository;
  final String _fallbackPolicyVersion;

  /// Load the stored decision and reconcile it with the server when reachable.
  Future<void> load() async {
    final stored = await _store.read();
    var version = stored?.policyVersion ?? _fallbackPolicyVersion;

    try {
      final status = await _repository.consent();
      version = status.policyVersion;
      if (stored != null && !stored.synced) {
        await _push(stored, version);
      }
    } catch (_) {
      // Signed out or offline: the local decision stands until we can sync it.
    }

    emit(ConsentState(
      decision: stored,
      policyVersion: version,
      prompting: stored == null || stored.policyVersion != version,
    ));
  }

  /// Accept every optional purpose.
  Future<void> acceptAll() => decide(ctxOptionalPurposes);

  /// Decline everything optional; essential processing continues.
  Future<void> essentialOnly() => decide(const {});

  /// Record a decision over the current notice version.
  Future<void> decide(Set<String> purposes) async {
    final version = state.policyVersion ?? _fallbackPolicyVersion;
    var decision = ConsentDecision(
      policyVersion: version,
      purposes: purposes,
      decidedAt: DateTime.now().toUtc(),
    );
    await _store.write(decision);
    emit(state.copyWith(decision: decision, prompting: false));

    final synced = await _push(decision, version);
    if (synced != null) {
      emit(state.copyWith(decision: synced, prompting: false));
    }
  }

  /// Send a decision to the server's audit trail, marking it synced on success.
  /// Returns null when it could not be delivered (signed out or offline).
  Future<ConsentDecision?> _push(ConsentDecision decision, String policyVersion) async {
    try {
      await _repository.recordConsent(policyVersion: policyVersion, purposes: decision.purposes);
      final synced = decision.copyWith(synced: true);
      await _store.write(synced);
      return synced;
    } catch (_) {
      return null;
    }
  }
}
