import 'package:bloc/bloc.dart';
import 'package:bloc_concurrency/bloc_concurrency.dart';
import 'package:equatable/equatable.dart';

import '../../../core/result/result.dart';
import '../../../core/utils/app_exception.dart';
import '../../../models/item.dart';
import '../data/items_repository.dart';
import '../data/location_service.dart';

part 'map_event.dart';
part 'map_state.dart';

/// Map screen Bloc: resolve location → fetch nearby items. Loads are
/// restartable — only the latest query matters when the user moves or
/// refreshes (docs/STATE_MANAGEMENT.md §4).
class MapBloc extends Bloc<MapEvent, MapState> {
  MapBloc({
    required ItemsRepository itemsRepository,
    required LocationService locationService,
  })  : _items = itemsRepository,
        _location = locationService,
        super(const MapState()) {
    on<MapOpened>(_onLoad, transformer: restartable());
    on<MapRefreshRequested>(_onLoad, transformer: restartable());
  }

  final ItemsRepository _items;
  final LocationService _location;

  Future<void> _onLoad(MapEvent event, Emitter<MapState> emit) async {
    emit(state.copyWith(status: MapStatus.loading));

    final position = await _location.getCurrentPosition();
    if (position == null) {
      // No location: show the default viewport without markers rather
      // than an error — permission is the user's choice.
      emit(state.copyWith(
        status: MapStatus.locationUnavailable,
        items: const [],
      ));
      return;
    }

    final result = await _items.getNearby(
      latitude: position.latitude,
      longitude: position.longitude,
      forceRefresh: event is MapRefreshRequested,
    );
    switch (result) {
      case Success(:final value):
        emit(state.copyWith(
          status: MapStatus.success,
          latitude: position.latitude,
          longitude: position.longitude,
          items: value,
        ));
      case Failure(:final error):
        emit(state.copyWith(
          status: MapStatus.failure,
          latitude: position.latitude,
          longitude: position.longitude,
          errorMessage: AppException.from(error).userFriendlyMessage,
        ));
    }
  }
}
