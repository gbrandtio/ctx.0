import 'package:equatable/equatable.dart';

/// One row of the in-app notification feed, backed by the API's
/// `user_notifications` outbox (api-template/docs/features/NOTIFICATIONS.md
/// §4). The `type` discriminator tells the client how to render/handle
/// the payload; unknown types render as plain title/body.
class AppNotification extends Equatable {
  const AppNotification({
    required this.id,
    required this.type,
    required this.title,
    required this.body,
    required this.createdAt,
  });

  factory AppNotification.fromJson(Map<String, dynamic> json) {
    return AppNotification(
      id: json['id'] as String,
      type: json['type'] as String,
      title: json['title'] as String,
      body: json['body'] as String? ?? '',
      createdAt: DateTime.parse(json['createdAt'] as String),
    );
  }

  final String id;
  final String type;
  final String title;
  final String body;
  final DateTime createdAt;

  @override
  List<Object?> get props => [id, type, title, body, createdAt];
}

/// A page of the feed. `hasMore` drives infinite scrolling.
class NotificationPage extends Equatable {
  const NotificationPage({required this.items, required this.hasMore});

  factory NotificationPage.fromJson(Map<String, dynamic> json) {
    return NotificationPage(
      items: [
        for (final item in json['items'] as List<dynamic>)
          AppNotification.fromJson(item as Map<String, dynamic>),
      ],
      hasMore: json['hasMore'] as bool? ?? false,
    );
  }

  final List<AppNotification> items;
  final bool hasMore;

  @override
  List<Object?> get props => [items, hasMore];
}
