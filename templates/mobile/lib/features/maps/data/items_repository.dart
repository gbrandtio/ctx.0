import '../../../core/result/result.dart';
import '../../../models/item.dart';
import 'item_api_service.dart';

/// SSOT for nearby geo-tagged items.
class ItemsRepository {
  ItemsRepository({required ItemApiService api}) : _api = api;

  final ItemApiService _api;

  Future<Result<List<Item>>> getNearby({
    required double latitude,
    required double longitude,
    double radiusKm = 10,
    bool forceRefresh = false,
  }) async {
    try {
      return Result.success(
        await _api.getNearby(
          latitude: latitude,
          longitude: longitude,
          radiusKm: radiusKm,
          forceRefresh: forceRefresh,
        ),
      );
    } on Exception catch (e) {
      return Result.failure(e);
    }
  }
}
