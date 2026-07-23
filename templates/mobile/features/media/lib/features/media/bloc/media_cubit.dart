import 'dart:typed_data';

import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../data/media_repository.dart';

enum MediaStatus { idle, loading, ready, failure }

/// Immutable state for the media screen; the view rebuilds purely from it.
final class MediaState extends Equatable {
  const MediaState({
    this.status = MediaStatus.idle,
    this.items = const [],
    this.uploading = false,
    this.error,
  });

  final MediaStatus status;
  final List<MediaItem> items;
  final bool uploading;
  final String? error;

  MediaState copyWith({
    MediaStatus? status,
    List<MediaItem>? items,
    bool? uploading,
    String? error,
  }) => MediaState(
    status: status ?? this.status,
    items: items ?? this.items,
    uploading: uploading ?? this.uploading,
    error: error,
  );

  @override
  List<Object?> get props => [status, items, uploading, error];
}

/// Drives the media screen. All I/O lives here; the view only renders.
class MediaCubit extends Cubit<MediaState> {
  MediaCubit(this._repository) : super(const MediaState());

  final MediaRepository _repository;

  Future<void> load() async {
    emit(state.copyWith(status: MediaStatus.loading, error: null));
    try {
      final items = await _repository.list();
      emit(state.copyWith(status: MediaStatus.ready, items: items));
    } catch (e) {
      emit(state.copyWith(status: MediaStatus.failure, error: e.toString()));
    }
  }

  Future<void> upload({
    required String fileName,
    required String contentType,
    required Uint8List bytes,
  }) async {
    emit(state.copyWith(uploading: true, error: null));
    try {
      await _repository.upload(
        fileName: fileName,
        contentType: contentType,
        bytes: bytes,
      );
      final items = await _repository.list();
      emit(
        state.copyWith(
          status: MediaStatus.ready,
          items: items,
          uploading: false,
        ),
      );
    } catch (e) {
      emit(
        state.copyWith(
          status: MediaStatus.failure,
          uploading: false,
          error: e.toString(),
        ),
      );
    }
  }

  Future<void> delete(String id) async {
    try {
      await _repository.delete(id);
      final items = await _repository.list();
      emit(state.copyWith(status: MediaStatus.ready, items: items));
    } catch (e) {
      emit(state.copyWith(status: MediaStatus.failure, error: e.toString()));
    }
  }
}
