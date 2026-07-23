import 'dart:io';
import 'dart:typed_data';

import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:path_provider/path_provider.dart';

import '../data/privacy_repository.dart';

enum PrivacyStatus { idle, exporting, exported, deleting, deleted, failure }

/// State of the privacy screen's two long-running actions: taking a copy of the
/// account's data, and deleting the account.
final class PrivacyState extends Equatable {
  const PrivacyState({
    this.status = PrivacyStatus.idle,
    this.job,
    this.archivePath,
    this.error,
  });

  final PrivacyStatus status;

  /// The export in flight, or the last one requested.
  final ExportJob? job;

  /// Where the finished archive was written on the device.
  final String? archivePath;

  final String? error;

  PrivacyState copyWith({
    PrivacyStatus? status,
    ExportJob? job,
    String? archivePath,
    String? error,
  }) => PrivacyState(
    status: status ?? this.status,
    job: job ?? this.job,
    archivePath: archivePath ?? this.archivePath,
    error: error,
  );

  @override
  List<Object?> get props => [
    status,
    job?.jobId,
    job?.status,
    archivePath,
    error,
  ];
}

/// Drives "download my data" and "delete my account". The export is built on the
/// server, so this polls the job until it is ready, downloads it with the
/// one-time token, and writes the archive into the app's documents directory.
class PrivacyCubit extends Cubit<PrivacyState> {
  PrivacyCubit(
    this._repository, {
    Duration pollInterval = const Duration(seconds: 2),
    int maxPolls = 60,
    Future<String> Function(String name, Uint8List bytes)? saveArchive,
  }) : _pollInterval = pollInterval,
       _maxPolls = maxPolls,
       _saveArchive = saveArchive ?? _writeToDocuments,
       super(const PrivacyState());

  final PrivacyRepository _repository;
  final Duration _pollInterval;
  final int _maxPolls;
  final Future<String> Function(String name, Uint8List bytes) _saveArchive;

  /// Request an export, wait for the server to build it, and save it locally.
  Future<void> downloadMyData() async {
    emit(const PrivacyState(status: PrivacyStatus.exporting));
    try {
      final requested = await _repository.requestExport();
      var job = requested.job;
      emit(state.copyWith(job: job));

      for (var poll = 0; job.isPending && poll < _maxPolls; poll++) {
        await Future<void>.delayed(_pollInterval);
        job = await _repository.exportStatus(job.jobId);
        emit(state.copyWith(job: job));
      }

      if (!job.isReady) {
        throw PrivacyException(
          job.error ??
              'The export is taking longer than expected — try again shortly.',
        );
      }

      final bytes = await _repository.downloadExport(
        jobId: job.jobId,
        downloadToken: requested.downloadToken,
      );
      final path = await _saveArchive('ctx-export-${job.jobId}.zip', bytes);

      emit(
        state.copyWith(
          status: PrivacyStatus.exported,
          job: job,
          archivePath: path,
        ),
      );
    } catch (e) {
      emit(state.copyWith(status: PrivacyStatus.failure, error: e.toString()));
    }
  }

  /// Erase the account. On success the session is dead and the app should return
  /// the user to the sign-in screen.
  Future<void> deleteAccount(String password) async {
    emit(const PrivacyState(status: PrivacyStatus.deleting));
    try {
      await _repository.deleteAccount(password: password);
      emit(const PrivacyState(status: PrivacyStatus.deleted));
    } catch (e) {
      emit(state.copyWith(status: PrivacyStatus.failure, error: e.toString()));
    }
  }

  static Future<String> _writeToDocuments(String name, Uint8List bytes) async {
    final dir = await getApplicationDocumentsDirectory();
    final file = File('${dir.path}/$name');
    await file.writeAsBytes(bytes, flush: true);
    return file.path;
  }
}
