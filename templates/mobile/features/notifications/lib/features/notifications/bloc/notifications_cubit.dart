import 'package:equatable/equatable.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../data/notifications_repository.dart';
import '../data/push_service.dart';

enum NotificationsStatus { idle, loading, ready, failure }

/// Immutable state for the notifications screen; the view rebuilds purely from it.
final class NotificationsState extends Equatable {
  const NotificationsState({
    this.status = NotificationsStatus.idle,
    this.items = const [],
    this.unreadCount = 0,
    this.error,
  });

  final NotificationsStatus status;
  final List<NotificationItem> items;
  final int unreadCount;
  final String? error;

  NotificationsState copyWith({
    NotificationsStatus? status,
    List<NotificationItem>? items,
    int? unreadCount,
    String? error,
  }) =>
      NotificationsState(
        status: status ?? this.status,
        items: items ?? this.items,
        unreadCount: unreadCount ?? this.unreadCount,
        error: error,
      );

  @override
  List<Object?> get props => [status, items, unreadCount, error];
}

/// Drives the notifications screen. All I/O lives here; the view only renders.
class NotificationsCubit extends Cubit<NotificationsState> {
  NotificationsCubit(this._repository, {PushService? push})
      : _push = push ?? const PushService(),
        super(const NotificationsState());

  final NotificationsRepository _repository;
  final PushService _push;

  /// Register for push (best effort) and load the current list.
  Future<void> init() async {
    await _push.register(_repository);
    await refresh();
  }

  Future<void> refresh() async {
    emit(state.copyWith(status: NotificationsStatus.loading, error: null));
    try {
      final items = await _repository.list();
      final unread = await _repository.unreadCount();
      emit(state.copyWith(status: NotificationsStatus.ready, items: items, unreadCount: unread));
    } catch (e) {
      emit(state.copyWith(status: NotificationsStatus.failure, error: e.toString()));
    }
  }

  Future<void> markRead(String id) async {
    try {
      await _repository.markRead(id);
      await refresh();
    } catch (e) {
      emit(state.copyWith(status: NotificationsStatus.failure, error: e.toString()));
    }
  }
}
