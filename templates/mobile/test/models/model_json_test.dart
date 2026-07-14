import 'package:app_template/models/app_notification.dart';
import 'package:app_template/models/item.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('fromJson id coercion', () {
    // The API serializes bigint ids as JSON numbers; the models must not
    // cast them as String (regression guard for the notifications-feed and
    // map-items crash).
    test('AppNotification.fromJson accepts a numeric id', () {
      final n = AppNotification.fromJson({
        'id': 7234567890123,
        'type': 'payment_completed',
        'title': 'Payment completed',
        'body': 'ok',
        'createdAt': '2026-07-14T00:00:00Z',
      });
      expect(n.id, '7234567890123');
    });

    test('Item.fromJson accepts a numeric id', () {
      final item = Item.fromJson({
        'id': 800001,
        'name': 'Near',
        'description': null,
        'latitude': 52.52,
        'longitude': 13.405,
        'distanceMeters': 1500,
      });
      expect(item.id, '800001');
      expect(item.latitude, 52.52);
      expect(item.distanceMeters, 1500.0);
    });
  });
}
