import 'package:app_template/core/models/problem_details.dart';
import 'package:app_template/core/result/result.dart';
import 'package:app_template/core/utils/app_exception.dart';
import 'package:app_template/features/notifications/bloc/notifications_bloc.dart';
import 'package:app_template/features/notifications/data/notifications_repository.dart';
import 'package:app_template/models/app_notification.dart';
import 'package:bloc_test/bloc_test.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

class _MockRepository extends Mock implements NotificationsRepository {}

AppNotification _notification(String id) => AppNotification(
      id: id,
      type: 'generic',
      title: 'Title $id',
      body: 'Body $id',
      createdAt: DateTime.utc(2026, 7, 10),
    );

void main() {
  late _MockRepository repository;

  setUp(() => repository = _MockRepository());

  NotificationsBloc build() => NotificationsBloc(repository: repository);

  blocTest<NotificationsBloc, NotificationsState>(
    'first fetch loads page 1 and advances the cursor',
    build: () {
      when(() => repository.getFeed(page: 1)).thenAnswer(
        (_) async => Result.success(
          NotificationPage(items: [_notification('1')], hasMore: true),
        ),
      );
      return build();
    },
    act: (bloc) => bloc.add(const NotificationsFetched()),
    expect: () => [
      isA<NotificationsState>()
          .having((s) => s.status, 'status', NotificationsStatus.loading),
      isA<NotificationsState>()
          .having((s) => s.items.length, 'items', 1)
          .having((s) => s.nextPage, 'nextPage', 2)
          .having((s) => s.hasReachedMax, 'hasReachedMax', false),
    ],
  );

  blocTest<NotificationsBloc, NotificationsState>(
    'stops fetching once the server reports no more pages',
    build: () {
      when(() => repository.getFeed(page: 1)).thenAnswer(
        (_) async => Result.success(
          NotificationPage(items: [_notification('1')], hasMore: false),
        ),
      );
      return build();
    },
    act: (bloc) => bloc
      ..add(const NotificationsFetched())
      ..add(const NotificationsFetched()),
    wait: const Duration(milliseconds: 20),
    verify: (_) =>
        verify(() => repository.getFeed(page: 1)).called(1),
  );

  blocTest<NotificationsBloc, NotificationsState>(
    'refresh replaces the list instead of appending (no duplicates)',
    seed: () => NotificationsState(
      status: NotificationsStatus.success,
      items: [_notification('old')],
      nextPage: 3,
    ),
    build: () {
      when(() => repository.getFeed(page: 1)).thenAnswer(
        (_) async => Result.success(
          NotificationPage(items: [_notification('new')], hasMore: true),
        ),
      );
      return build();
    },
    act: (bloc) => bloc.add(const NotificationsRefreshRequested()),
    expect: () => [
      isA<NotificationsState>()
          .having((s) => s.items.single.id, 'only item', 'new')
          .having((s) => s.nextPage, 'nextPage', 2),
    ],
  );

  blocTest<NotificationsBloc, NotificationsState>(
    'failure keeps already-loaded items visible',
    seed: () => NotificationsState(
      status: NotificationsStatus.success,
      items: [_notification('1')],
      nextPage: 2,
    ),
    build: () {
      when(() => repository.getFeed(page: 2)).thenAnswer(
        (_) async => const Result.failure(
          AppException(ProblemDetails(status: 500)),
        ),
      );
      return build();
    },
    act: (bloc) => bloc.add(const NotificationsFetched()),
    expect: () => [
      isA<NotificationsState>()
          .having((s) => s.status, 'status', NotificationsStatus.loading),
      isA<NotificationsState>()
          .having((s) => s.status, 'status', NotificationsStatus.failure)
          .having((s) => s.items.length, 'items preserved', 1),
    ],
  );
}
