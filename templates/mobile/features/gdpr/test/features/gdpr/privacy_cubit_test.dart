import 'dart:convert';
import 'dart:typed_data';

import 'package:bloc_test/bloc_test.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:ctxapp/features/gdpr/bloc/privacy_cubit.dart';
import 'package:ctxapp/features/gdpr/data/privacy_repository.dart';

/// Fake API that finishes an export after [pendingPolls] status checks, so the
/// cubit's polling loop is exercised without a server.
class FakeExportRepository implements PrivacyRepository {
  FakeExportRepository({this.pendingPolls = 1, this.deleteFails = false});

  final int pendingPolls;
  final bool deleteFails;
  int polls = 0;
  String? tokenIssued;
  String? tokenPresented;
  String? passwordPresented;

  @override
  Future<({ExportJob job, String downloadToken})> requestExport() async {
    tokenIssued = 'one-time-token';
    return (
      job: const ExportJob(jobId: 'job-1', status: 'Pending'),
      downloadToken: tokenIssued!,
    );
  }

  @override
  Future<ExportJob> exportStatus(String jobId) async {
    polls++;
    return polls > pendingPolls
        ? const ExportJob(jobId: 'job-1', status: 'Ready', sizeBytes: 12)
        : const ExportJob(jobId: 'job-1', status: 'Pending');
  }

  @override
  Future<Uint8List> downloadExport({
    required String jobId,
    required String downloadToken,
  }) async {
    tokenPresented = downloadToken;
    return Uint8List.fromList(utf8.encode('archive-bytes'));
  }

  @override
  Future<void> deleteAccount({required String password}) async {
    passwordPresented = password;
    if (deleteFails) throw const PrivacyException('Password does not match.');
  }

  @override
  Future<ConsentStatus> consent() async => throw UnimplementedError();

  @override
  Future<ConsentStatus> recordConsent({
    required String policyVersion,
    required Set<String> purposes,
  }) async => throw UnimplementedError();
}

/// Captures the archive instead of writing to the device's documents directory.
class ArchiveSink {
  final Map<String, Uint8List> saved = {};

  Future<String> call(String name, Uint8List bytes) async {
    saved[name] = bytes;
    return '/tmp/$name';
  }
}

void main() {
  test('downloadMyData polls until ready, then saves the archive', () async {
    final api = FakeExportRepository(pendingPolls: 2);
    final sink = ArchiveSink();
    final cubit = PrivacyCubit(
      api,
      pollInterval: Duration.zero,
      saveArchive: sink.call,
    );

    await cubit.downloadMyData();

    expect(cubit.state.status, PrivacyStatus.exported);
    expect(cubit.state.archivePath, '/tmp/ctx-export-job-1.zip');
    expect(api.polls, 3, reason: 'two pending checks, then ready');
    expect(
      api.tokenPresented,
      api.tokenIssued,
      reason: 'the one-time token from the request is what downloads it',
    );
    expect(utf8.decode(sink.saved['ctx-export-job-1.zip']!), 'archive-bytes');
  });

  test(
    'an export that never becomes ready fails rather than hanging',
    () async {
      final cubit = PrivacyCubit(
        FakeExportRepository(pendingPolls: 99),
        pollInterval: Duration.zero,
        maxPolls: 3,
        saveArchive: ArchiveSink().call,
      );

      await cubit.downloadMyData();

      expect(cubit.state.status, PrivacyStatus.failure);
      expect(cubit.state.error, contains('longer than expected'));
    },
  );

  blocTest<PrivacyCubit, PrivacyState>(
    'deleteAccount sends the password and reports success',
    build: () =>
        PrivacyCubit(FakeExportRepository(), saveArchive: ArchiveSink().call),
    act: (cubit) => cubit.deleteAccount('correct horse'),
    expect: () => [
      const PrivacyState(status: PrivacyStatus.deleting),
      const PrivacyState(status: PrivacyStatus.deleted),
    ],
  );

  blocTest<PrivacyCubit, PrivacyState>(
    'a rejected password surfaces the server error',
    build: () => PrivacyCubit(
      FakeExportRepository(deleteFails: true),
      saveArchive: ArchiveSink().call,
    ),
    act: (cubit) => cubit.deleteAccount('wrong'),
    expect: () => [
      const PrivacyState(status: PrivacyStatus.deleting),
      isA<PrivacyState>()
          .having((s) => s.status, 'status', PrivacyStatus.failure)
          .having((s) => s.error, 'error', contains('does not match')),
    ],
  );
}
