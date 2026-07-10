part of 'map_bloc.dart';

enum MapStatus { initial, loading, success, locationUnavailable, failure }

class MapState extends Equatable {
  const MapState({
    this.status = MapStatus.initial,
    this.latitude,
    this.longitude,
    this.items = const [],
    this.errorMessage,
  });

  final MapStatus status;
  final double? latitude;
  final double? longitude;
  final List<Item> items;
  final String? errorMessage;

  MapState copyWith({
    MapStatus? status,
    double? latitude,
    double? longitude,
    List<Item>? items,
    String? errorMessage,
  }) {
    return MapState(
      status: status ?? this.status,
      latitude: latitude ?? this.latitude,
      longitude: longitude ?? this.longitude,
      items: items ?? this.items,
      errorMessage: errorMessage ?? this.errorMessage,
    );
  }

  @override
  List<Object?> get props =>
      [status, latitude, longitude, items, errorMessage];
}
