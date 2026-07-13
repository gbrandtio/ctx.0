import 'package:http/http.dart' as http;

import '../../../core/constants/api_constants.dart';
import 'package:ctx0_mobile_security/ctx0_mobile_security.dart';
import '../../../data/services/api/mixins/api_base_mixin.dart';
import '../../../models/item.dart';

/// Nearby query (templates/api/docs/features/SPATIAL_QUERIES.md): a GET
/// with query parameters so it is cacheable end to end — output cache on
/// the API, Hive cache here.
class ItemApiService with ApiBaseMixin {
  ItemApiService(this._client);

  final http.Client _client;

  /// Client-side mirror of the API's radius clamp (SPATIAL_QUERIES.md
  /// documents ~100 km max to bound query cost).
  static const double maxRadiusKm = 100;

  Future<List<Item>> getNearby({
    required double latitude,
    required double longitude,
    double radiusKm = 10,
    bool forceRefresh = false,
  }) async {
    final response = await _client.get(
      ApiConstants.uri(ApiConstants.itemsNearby, {
        'lat': latitude,
        'lng': longitude,
        'radiusKm': radiusKm.clamp(0, maxRadiusKm),
      }),
      headers: forceRefresh ? const {CachingClient.bypassHeader: 'true'} : null,
    );
    final json = decodeResponse(response) as List<dynamic>;
    return [
      for (final item in json) Item.fromJson(item as Map<String, dynamic>),
    ];
  }
}
