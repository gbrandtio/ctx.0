import 'dart:convert';

import 'package:http/http.dart' as http;

import '../../auth/data/token_store.dart';

/// One in-app notification as returned by the API.
class NotificationItem {
  const NotificationItem({
    required this.id,
    required this.title,
    required this.body,
    required this.read,
    required this.createdAt,
  });

  final String id;
  final String title;
  final String body;
  final bool read;
  final DateTime createdAt;

  factory NotificationItem.fromJson(Map<String, dynamic> json) => NotificationItem(
        id: json['id'] as String,
        title: json['title'] as String,
        body: json['body'] as String,
        read: json['readAt'] != null,
        createdAt: DateTime.parse(json['createdAt'] as String),
      );
}

/// Raised when a notifications request fails.
class NotificationsException implements Exception {
  const NotificationsException(this.message);
  final String message;
  @override
  String toString() => message;
}

/// Reads and updates the signed-in user's notifications.
abstract class NotificationsRepository {
  Future<List<NotificationItem>> list();
  Future<int> unreadCount();
  Future<void> markRead(String id);
  Future<void> registerDevice(String platform, String token);
  Future<void> unregisterDevice(String token);
}

/// [NotificationsRepository] backed by the JWT-protected `/v1/notifications`
/// endpoints. Notifications are per-user and RLS-isolated on the server, so every
/// request carries the access token minted by the auth feature. This uses plain
/// authenticated JSON (not the ALE `secureSend` client, which carries no user).
class HttpNotificationsRepository implements NotificationsRepository {
  HttpNotificationsRepository(this._tokens, {String? baseUrl, http.Client? client})
      : _baseUrl = baseUrl ?? const String.fromEnvironment('CTX_API_BASE_URL', defaultValue: 'http://localhost:5080'),
        _http = client ?? http.Client();

  final TokenStore _tokens;
  final String _baseUrl;
  final http.Client _http;

  @override
  Future<List<NotificationItem>> list() async {
    final json = await _get('/v1/notifications/');
    final items = (json['items'] as List<dynamic>).cast<Map<String, dynamic>>();
    return items.map(NotificationItem.fromJson).toList();
  }

  @override
  Future<int> unreadCount() async {
    final json = await _get('/v1/notifications/unread-count');
    return json['count'] as int;
  }

  @override
  Future<void> markRead(String id) async {
    await _send('POST', '/v1/notifications/$id/read');
  }

  @override
  Future<void> registerDevice(String platform, String token) async {
    await _send('POST', '/v1/notifications/devices', {'platform': platform, 'token': token});
  }

  @override
  Future<void> unregisterDevice(String token) async {
    await _send('DELETE', '/v1/notifications/devices', {'token': token});
  }

  Future<Map<String, dynamic>> _get(String path) async {
    final response = await _http.get(Uri.parse('$_baseUrl$path'), headers: await _headers());
    return _decode(response);
  }

  Future<Map<String, dynamic>> _send(String method, String path, [Map<String, dynamic>? body]) async {
    final request = http.Request(method, Uri.parse('$_baseUrl$path'))
      ..headers.addAll(await _headers());
    if (body != null) request.body = jsonEncode(body);
    final response = await http.Response.fromStream(await _http.send(request));
    return _decode(response);
  }

  Future<Map<String, String>> _headers() async {
    final token = await _tokens.readAccessToken();
    if (token == null) throw const NotificationsException('Not signed in');
    return {'Content-Type': 'application/json', 'Authorization': 'Bearer $token'};
  }

  Map<String, dynamic> _decode(http.Response response) {
    if (response.statusCode >= 400) {
      throw NotificationsException('Request failed (${response.statusCode})');
    }
    if (response.body.isEmpty) return const {};
    return jsonDecode(response.body) as Map<String, dynamic>;
  }
}
