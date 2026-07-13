import 'package:app_template/core/models/problem_details.dart';
import 'package:app_template/core/result/result.dart';
import 'package:app_template/core/utils/app_exception.dart';
import 'package:app_template/features/maps/bloc/map_bloc.dart';
import 'package:app_template/features/maps/data/items_repository.dart';
import 'package:app_template/features/maps/data/location_service.dart';
import 'package:app_template/models/item.dart';
import 'package:bloc_test/bloc_test.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

class _MockItemsRepository extends Mock implements ItemsRepository {}

class _MockLocationService extends Mock implements LocationService {}

const _item = Item(id: 'i1', name: 'Item', latitude: 1, longitude: 2);

void main() {
  late _MockItemsRepository items;
  late _MockLocationService location;

  setUp(() {
    items = _MockItemsRepository();
    location = _MockLocationService();
  });

  MapBloc build() =>
      MapBloc(itemsRepository: items, locationService: location);

  blocTest<MapBloc, MapState>(
    'loads nearby items around the resolved location',
    build: () {
      when(() => location.getCurrentPosition())
          .thenAnswer((_) async => (latitude: 1.0, longitude: 2.0));
      when(() => items.getNearby(
            latitude: 1.0,
            longitude: 2.0,
            forceRefresh: false,
          )).thenAnswer((_) async => const Result.success([_item]));
      return build();
    },
    act: (bloc) => bloc.add(const MapOpened()),
    expect: () => [
      isA<MapState>().having((s) => s.status, 'status', MapStatus.loading),
      isA<MapState>()
          .having((s) => s.status, 'status', MapStatus.success)
          .having((s) => s.items, 'items', [_item])
          .having((s) => s.latitude, 'latitude', 1.0),
    ],
  );

  blocTest<MapBloc, MapState>(
    'denied location degrades to locationUnavailable — not an error',
    build: () {
      when(() => location.getCurrentPosition()).thenAnswer((_) async => null);
      return build();
    },
    act: (bloc) => bloc.add(const MapOpened()),
    expect: () => [
      isA<MapState>().having((s) => s.status, 'status', MapStatus.loading),
      isA<MapState>().having(
          (s) => s.status, 'status', MapStatus.locationUnavailable),
    ],
    verify: (_) => verifyNever(() => items.getNearby(
          latitude: any(named: 'latitude'),
          longitude: any(named: 'longitude'),
          forceRefresh: any(named: 'forceRefresh'),
        )),
  );

  blocTest<MapBloc, MapState>(
    'refresh bypasses the HTTP cache',
    build: () {
      when(() => location.getCurrentPosition())
          .thenAnswer((_) async => (latitude: 1.0, longitude: 2.0));
      when(() => items.getNearby(
            latitude: 1.0,
            longitude: 2.0,
            forceRefresh: true,
          )).thenAnswer((_) async => const Result.success([_item]));
      return build();
    },
    act: (bloc) => bloc.add(const MapRefreshRequested()),
    verify: (_) => verify(() => items.getNearby(
          latitude: 1.0,
          longitude: 2.0,
          forceRefresh: true,
        )).called(1),
  );

  blocTest<MapBloc, MapState>(
    'API failure keeps the viewport and reports a safe message',
    build: () {
      when(() => location.getCurrentPosition())
          .thenAnswer((_) async => (latitude: 1.0, longitude: 2.0));
      when(() => items.getNearby(
            latitude: 1.0,
            longitude: 2.0,
            forceRefresh: false,
          )).thenAnswer(
        (_) async =>
            const Result.failure(AppException(ProblemDetails(status: 500))),
      );
      return build();
    },
    act: (bloc) => bloc.add(const MapOpened()),
    expect: () => [
      isA<MapState>().having((s) => s.status, 'status', MapStatus.loading),
      isA<MapState>()
          .having((s) => s.status, 'status', MapStatus.failure)
          .having((s) => s.latitude, 'latitude kept', 1.0),
    ],
  );
}
