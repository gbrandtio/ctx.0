import 'dart:typed_data';

import 'package:bloc_test/bloc_test.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:ctxapp/features/gdpr/bloc/consent_cubit.dart';
import 'package:ctxapp/features/gdpr/data/consent_store.dart';
import 'package:ctxapp/features/gdpr/data/privacy_repository.dart';

/// In-memory consent storage, standing in for the device's secure storage.
class FakeConsentStore implements ConsentStore {
  FakeConsentStore([this._decision]);

  ConsentDecision? _decision;

  @override
  Future<ConsentDecision?> read() async => _decision;

  @override
  Future<void> write(ConsentDecision decision) async => _decision = decision;

  @override
  Future<void> clear() async => _decision = null;
}

/// Fake API. [signedIn] false makes every call throw, as it does when there is
/// no session — the case the local-first consent flow exists for.
class FakePrivacyRepository implements PrivacyRepository {
  FakePrivacyRepository({this.policyVersion = '1', this.signedIn = true});

  final String policyVersion;
  final bool signedIn;
  final List<Set<String>> recorded = [];

  @override
  Future<ConsentStatus> consent() async {
    if (!signedIn) throw const PrivacyException('Not signed in');
    return ConsentStatus(policyVersion: policyVersion);
  }

  @override
  Future<ConsentStatus> recordConsent({
    required String policyVersion,
    required Set<String> purposes,
  }) async {
    if (!signedIn) throw const PrivacyException('Not signed in');
    recorded.add(purposes);
    return ConsentStatus(
      policyVersion: policyVersion,
      purposes: purposes,
      recordedVersion: policyVersion,
      decidedAt: DateTime.utc(2024),
    );
  }

  @override
  Future<({ExportJob job, String downloadToken})> requestExport() async =>
      throw UnimplementedError();

  @override
  Future<ExportJob> exportStatus(String jobId) async =>
      throw UnimplementedError();

  @override
  Future<Uint8List> downloadExport({
    required String jobId,
    required String downloadToken,
  }) async => throw UnimplementedError();

  @override
  Future<void> deleteAccount({required String password}) async =>
      throw UnimplementedError();
}

void main() {
  blocTest<ConsentCubit, ConsentState>(
    'prompts when nothing has been decided yet',
    build: () => ConsentCubit(FakeConsentStore(), FakePrivacyRepository()),
    act: (cubit) => cubit.load(),
    expect: () => [
      isA<ConsentState>()
          .having((s) => s.prompting, 'prompting', true)
          .having((s) => s.policyVersion, 'policyVersion', '1'),
    ],
  );

  blocTest<ConsentCubit, ConsentState>(
    're-prompts when the notice version has moved on',
    build: () => ConsentCubit(
      FakeConsentStore(
        ConsentDecision(
          policyVersion: '1',
          purposes: const {'analytics'},
          decidedAt: DateTime.utc(2024),
          synced: true,
        ),
      ),
      FakePrivacyRepository(policyVersion: '2'),
    ),
    act: (cubit) => cubit.load(),
    expect: () => [
      isA<ConsentState>()
          .having((s) => s.prompting, 'prompting', true)
          .having((s) => s.policyVersion, 'policyVersion', '2'),
    ],
  );

  blocTest<ConsentCubit, ConsentState>(
    'accepting stores the decision and stops prompting',
    build: () => ConsentCubit(FakeConsentStore(), FakePrivacyRepository()),
    act: (cubit) async {
      await cubit.load();
      await cubit.acceptAll();
    },
    skip: 1,
    expect: () => [
      isA<ConsentState>()
          .having((s) => s.prompting, 'prompting', false)
          .having((s) => s.decision?.purposes, 'purposes', ctxOptionalPurposes),
      isA<ConsentState>().having((s) => s.decision?.synced, 'synced', true),
    ],
  );

  test(
    'a decision made while signed out is kept locally and synced on the next load',
    () async {
      final store = FakeConsentStore();
      final offline = ConsentCubit(
        store,
        FakePrivacyRepository(signedIn: false),
      );
      await offline.load();
      await offline.essentialOnly();

      final stored = await store.read();
      expect(stored, isNotNull);
      expect(stored!.purposes, isEmpty);
      expect(
        stored.synced,
        isFalse,
        reason: 'no session, so the server has not seen it yet',
      );

      final api = FakePrivacyRepository();
      final signedIn = ConsentCubit(store, api);
      await signedIn.load();

      expect(api.recorded, [<String>{}]);
      expect((await store.read())!.synced, isTrue);
      expect(signedIn.state.prompting, isFalse);
    },
  );
}
