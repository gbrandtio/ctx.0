import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:intl/intl.dart';

import '../../../core/l10n/l10n.dart';
import '../../../core/widgets/app_header.dart';
import '../bloc/notifications_bloc.dart';

class NotificationsScreen extends StatefulWidget {
  const NotificationsScreen({super.key});

  @override
  State<NotificationsScreen> createState() => _NotificationsScreenState();
}

class _NotificationsScreenState extends State<NotificationsScreen> {
  final _scrollController = ScrollController();

  @override
  void initState() {
    super.initState();
    _scrollController.addListener(_onScroll);
  }

  @override
  void dispose() {
    _scrollController
      ..removeListener(_onScroll)
      ..dispose();
    super.dispose();
  }

  void _onScroll() {
    final position = _scrollController.position;
    // Prefetch the next page shortly before the end of the list; the
    // droppable transformer absorbs repeated triggers.
    if (position.pixels >= position.maxScrollExtent * 0.9) {
      context.read<NotificationsBloc>().add(const NotificationsFetched());
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppHeader(
        config: HeaderConfig(
          title: (context) => context.l10n.notificationsTitle,
          showBackButton: false,
        ),
      ),
      body: BlocConsumer<NotificationsBloc, NotificationsState>(
        listenWhen: (previous, current) =>
            current.status == NotificationsStatus.failure &&
            previous.status != NotificationsStatus.failure,
        listener: (context, state) {
          final message = state.errorMessage;
          if (message != null) {
            ScaffoldMessenger.of(context)
              ..hideCurrentSnackBar()
              ..showSnackBar(SnackBar(content: Text(message)));
          }
        },
        builder: (context, state) {
          if (state.status == NotificationsStatus.initial ||
              (state.items.isEmpty &&
                  state.status == NotificationsStatus.loading)) {
            return const Center(child: CircularProgressIndicator());
          }
          if (state.items.isEmpty) {
            return Center(child: Text(context.l10n.notificationsEmpty));
          }
          return RefreshIndicator(
            onRefresh: () async => context
                .read<NotificationsBloc>()
                .add(const NotificationsRefreshRequested()),
            child: ListView.builder(
              controller: _scrollController,
              physics: const AlwaysScrollableScrollPhysics(),
              itemCount:
                  state.items.length + (state.hasReachedMax ? 0 : 1),
              itemBuilder: (context, index) {
                if (index >= state.items.length) {
                  return const Padding(
                    padding: EdgeInsets.all(16),
                    child: Center(child: CircularProgressIndicator()),
                  );
                }
                final notification = state.items[index];
                return Card(
                  child: ListTile(
                    leading: const Icon(Icons.notifications_outlined),
                    title: Text(notification.title),
                    subtitle: Text(notification.body),
                    trailing: Text(
                      DateFormat.yMMMd(
                        Localizations.localeOf(context).toString(),
                      ).format(notification.createdAt.toLocal()),
                      style: Theme.of(context).textTheme.bodySmall,
                    ),
                  ),
                );
              },
            ),
          );
        },
      ),
    );
  }
}
