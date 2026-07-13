part of 'map_bloc.dart';

sealed class MapEvent {
  const MapEvent();
}

final class MapOpened extends MapEvent {
  const MapOpened();
}

/// Pull-style refresh: re-resolves location and bypasses the HTTP cache.
final class MapRefreshRequested extends MapEvent {
  const MapRefreshRequested();
}
