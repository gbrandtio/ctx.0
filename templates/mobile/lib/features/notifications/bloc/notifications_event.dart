part of 'notifications_bloc.dart';

sealed class NotificationsEvent {
  const NotificationsEvent();
}

/// Load the next page (initial load and infinite scroll).
final class NotificationsFetched extends NotificationsEvent {
  const NotificationsFetched();
}

/// Pull-to-refresh: restart from page 1.
final class NotificationsRefreshRequested extends NotificationsEvent {
  const NotificationsRefreshRequested();
}
