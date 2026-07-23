import 'package:bloc_test/bloc_test.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:ctxapp/features/notifications/bloc/notifications_cubit.dart';
import 'package:ctxapp/features/notifications/data/notifications_repository.dart';
import 'package:ctxapp/features/notifications/data/push_service.dart';

/// In-memory fake so the cubit can be tested without HTTP.
class FakeNotificationsRepository implements NotificationsRepository {
  FakeNotificationsRepository(this._items);

  final List<NotificationItem> _items;
  final List<String> readIds = [];

  @override
  Future<List<NotificationItem>> list() async => _items;

  @override
  Future<int> unreadCount() async => _items.where((n) => !n.read).length;

  @override
  Future<void> markRead(String id) async => readIds.add(id);

  @override
  Future<void> registerDevice(String platform, String token) async {}

  @override
  Future<void> unregisterDevice(String token) async {}
}

/// No-op push so tests never touch Firebase platform channels.
class NoopPushService extends PushService {
  const NoopPushService();
  @override
  Future<void> register(NotificationsRepository repository) async {}
}

NotificationItem _item(String id, {bool read = false}) => NotificationItem(
  id: id,
  title: 'Title $id',
  body: 'Body $id',
  read: read,
  createdAt: DateTime(2026, 1, 1),
);

void main() {
  blocTest<NotificationsCubit, NotificationsState>(
    'refresh loads items and unread count',
    build: () => NotificationsCubit(
      FakeNotificationsRepository([_item('1'), _item('2', read: true)]),
      push: const NoopPushService(),
    ),
    act: (cubit) => cubit.refresh(),
    expect: () => [
      const NotificationsState(status: NotificationsStatus.loading),
      isA<NotificationsState>()
          .having((s) => s.status, 'status', NotificationsStatus.ready)
          .having((s) => s.items.length, 'items', 2)
          .having((s) => s.unreadCount, 'unreadCount', 1),
    ],
  );

  blocTest<NotificationsCubit, NotificationsState>(
    'markRead marks the item then refreshes',
    build: () => NotificationsCubit(
      FakeNotificationsRepository([_item('1')]),
      push: const NoopPushService(),
    ),
    act: (cubit) => cubit.markRead('1'),
    verify: (cubit) {
      expect((cubit).state.status, NotificationsStatus.ready);
    },
  );
}
