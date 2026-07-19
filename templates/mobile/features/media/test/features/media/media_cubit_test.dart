import 'dart:typed_data';

import 'package:bloc_test/bloc_test.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:ctxapp/features/media/bloc/media_cubit.dart';
import 'package:ctxapp/features/media/data/media_repository.dart';

/// In-memory fake so the cubit can be tested without HTTP.
class FakeMediaRepository implements MediaRepository {
  FakeMediaRepository(this._items);

  final List<MediaItem> _items;
  final List<String> deletedIds = [];
  int uploads = 0;

  @override
  Future<List<MediaItem>> list() async => List.of(_items);

  @override
  Future<MediaItem> upload({required String fileName, required String contentType, required Uint8List bytes}) async {
    uploads++;
    final item = _item(fileName, contentType: contentType, size: bytes.length);
    _items.add(item);
    return item;
  }

  @override
  Future<void> delete(String id) async {
    deletedIds.add(id);
    _items.removeWhere((m) => m.id == id);
  }

  @override
  Uri downloadUri(String id) => Uri.parse('http://localhost/v1/media/$id');
}

MediaItem _item(String id, {String contentType = 'image/png', int size = 3}) => MediaItem(
      id: id,
      fileName: '$id.png',
      contentType: contentType,
      sizeBytes: size,
      createdAt: DateTime(2026, 1, 1),
    );

void main() {
  blocTest<MediaCubit, MediaState>(
    'load fetches the current items',
    build: () => MediaCubit(FakeMediaRepository([_item('1'), _item('2')])),
    act: (cubit) => cubit.load(),
    expect: () => [
      const MediaState(status: MediaStatus.loading),
      isA<MediaState>()
          .having((s) => s.status, 'status', MediaStatus.ready)
          .having((s) => s.items.length, 'items', 2),
    ],
  );

  blocTest<MediaCubit, MediaState>(
    'upload stores the file then reloads the list',
    build: () => MediaCubit(FakeMediaRepository([_item('1')])),
    act: (cubit) => cubit.upload(fileName: 'new.png', contentType: 'image/png', bytes: Uint8List.fromList([1, 2, 3])),
    expect: () => [
      const MediaState(uploading: true),
      isA<MediaState>()
          .having((s) => s.status, 'status', MediaStatus.ready)
          .having((s) => s.uploading, 'uploading', false)
          .having((s) => s.items.length, 'items', 2),
    ],
  );

  blocTest<MediaCubit, MediaState>(
    'delete removes the file then reloads',
    build: () => MediaCubit(FakeMediaRepository([_item('1'), _item('2')])),
    act: (cubit) => cubit.delete('1'),
    verify: (cubit) {
      expect(cubit.state.status, MediaStatus.ready);
      expect(cubit.state.items.length, 1);
    },
  );
}
