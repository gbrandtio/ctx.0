part of 'notifications_bloc.dart';

enum NotificationsStatus { initial, loading, success, failure }

class NotificationsState extends Equatable {
  const NotificationsState({
    this.status = NotificationsStatus.initial,
    this.items = const [],
    this.nextPage = 1,
    this.hasReachedMax = false,
    this.errorMessage,
  });

  final NotificationsStatus status;
  final List<AppNotification> items;
  final int nextPage;
  final bool hasReachedMax;
  final String? errorMessage;

  NotificationsState copyWith({
    NotificationsStatus? status,
    List<AppNotification>? items,
    int? nextPage,
    bool? hasReachedMax,
    String? errorMessage,
  }) {
    return NotificationsState(
      status: status ?? this.status,
      items: items ?? this.items,
      nextPage: nextPage ?? this.nextPage,
      hasReachedMax: hasReachedMax ?? this.hasReachedMax,
      errorMessage: errorMessage ?? this.errorMessage,
    );
  }

  @override
  List<Object?> get props =>
      [status, items, nextPage, hasReachedMax, errorMessage];
}
