import 'package:equatable/equatable.dart';

/// A geo-tagged item — the generic spatial example aggregate
/// (api-template/docs/features/SPATIAL_QUERIES.md). Rename/extend per
/// your domain; keep the shape of the nearby query.
class Item extends Equatable {
  const Item({
    required this.id,
    required this.name,
    this.description,
    required this.latitude,
    required this.longitude,
    this.distanceMeters,
  });

  factory Item.fromJson(Map<String, dynamic> json) {
    return Item(
      id: json['id'] as String,
      name: json['name'] as String,
      description: json['description'] as String?,
      latitude: (json['latitude'] as num).toDouble(),
      longitude: (json['longitude'] as num).toDouble(),
      distanceMeters: (json['distanceMeters'] as num?)?.toDouble(),
    );
  }

  final String id;
  final String name;
  final String? description;
  final double latitude;
  final double longitude;

  /// Server-computed geodesic distance from the query point.
  final double? distanceMeters;

  @override
  List<Object?> get props =>
      [id, name, description, latitude, longitude, distanceMeters];
}
