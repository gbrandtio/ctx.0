import 'dart:convert';

import 'package:http/http.dart' as http;

import '../../../core/constants/api_constants.dart';
import '../../../data/services/api/interceptors/caching_client.dart';
import '../../../data/services/api/mixins/api_base_mixin.dart';
import '../../../models/app_notification.dart';

/// Notification endpoints. The feed is NEVER client-cached — freshness
/// matters (docs/CACHING_IMPLEMENTATION.md "Mandatory Bypass";
/// api-template/docs/features/NOTIFICATIONS.md §4).
class NotificationApiService with ApiBaseMixin {
  NotificationApiService(this._client);

  final http.Client _client;

  static const _jsonHeaders = {'Content-Type': 'application/json'};
  static const _noCacheHeaders = {CachingClient.bypassHeader: 'true'};

  Future<void> registerFirebaseToken(String token) async {
    final response = await _client.post(
      ApiConstants.uri(ApiConstants.firebaseToken),
      headers: _jsonHeaders,
      body: jsonEncode({'token': token}),
    );
    decodeResponse(response);
  }

  /// Called on logout so a stale device stops receiving pushes
  /// (api-template/docs/features/NOTIFICATIONS.md §2).
  Future<void> unregisterFirebaseToken() async {
    final response = await _client.delete(
      ApiConstants.uri(ApiConstants.firebaseToken),
    );
    decodeResponse(response);
  }

  Future<NotificationPage> getNotifications({required int page}) async {
    final response = await _client.get(
      ApiConstants.uri(
        ApiConstants.notifications,
        {'page': page, 'pageSize': 20},
      ),
      headers: _noCacheHeaders,
    );
    return NotificationPage.fromJson(
      decodeResponse(response) as Map<String, dynamic>,
    );
  }
}
