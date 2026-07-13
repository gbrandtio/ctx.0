import 'package:bloc/bloc.dart';
import 'package:bloc_concurrency/bloc_concurrency.dart';
import 'package:equatable/equatable.dart';

import '../../../core/result/result.dart';
import '../../../core/utils/app_exception.dart';
import '../../../models/app_notification.dart';
import '../data/notifications_repository.dart';

part 'notifications_event.dart';
part 'notifications_state.dart';

/// In-app feed with infinite scroll. Data stays visible during refreshes,
/// so a single state class with status + hasReachedMax
/// (docs/STATE_MANAGEMENT.md §3). Page fetches are droppable — scroll
/// spam cannot double-fetch (§4).
class NotificationsBloc extends Bloc<NotificationsEvent, NotificationsState> {
  NotificationsBloc({required NotificationsRepository repository})
    : _repository = repository,
      super(const NotificationsState()) {
    on<NotificationsFetched>(_onFetched, transformer: droppable());
    on<NotificationsRefreshRequested>(_onRefresh, transformer: droppable());
  }

  final NotificationsRepository _repository;

  Future<void> _onFetched(
    NotificationsFetched event,
    Emitter<NotificationsState> emit,
  ) async {
    if (state.hasReachedMax) return;
    emit(state.copyWith(status: NotificationsStatus.loading));
    final result = await _repository.getFeed(page: state.nextPage);
    switch (result) {
      case Success(:final value):
        emit(
          state.copyWith(
            status: NotificationsStatus.success,
            items: [...state.items, ...value.items],
            nextPage: state.nextPage + 1,
            hasReachedMax: !value.hasMore,
          ),
        );
      case Failure(:final error):
        emit(
          state.copyWith(
            status: NotificationsStatus.failure,
            errorMessage: AppException.from(error).userFriendlyMessage,
          ),
        );
    }
  }

  Future<void> _onRefresh(
    NotificationsRefreshRequested event,
    Emitter<NotificationsState> emit,
  ) async {
    final result = await _repository.getFeed(page: 1);
    switch (result) {
      case Success(:final value):
        emit(
          NotificationsState(
            status: NotificationsStatus.success,
            items: value.items,
            nextPage: 2,
            hasReachedMax: !value.hasMore,
          ),
        );
      case Failure(:final error):
        emit(
          state.copyWith(
            status: NotificationsStatus.failure,
            errorMessage: AppException.from(error).userFriendlyMessage,
          ),
        );
    }
  }
}
