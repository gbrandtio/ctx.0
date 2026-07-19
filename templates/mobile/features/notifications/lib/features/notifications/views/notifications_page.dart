import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import '../bloc/notifications_cubit.dart';

/// Lists the signed-in user's notifications with an unread badge; tapping an
/// unread item marks it read.
class NotificationsPage extends StatefulWidget {
  const NotificationsPage({super.key});

  @override
  State<NotificationsPage> createState() => _NotificationsPageState();
}

class _NotificationsPageState extends State<NotificationsPage> {
  @override
  void initState() {
    super.initState();
    context.read<NotificationsCubit>().init();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('Notifications'),
        actions: [
          BlocBuilder<NotificationsCubit, NotificationsState>(
            builder: (context, state) => state.unreadCount == 0
                ? const SizedBox.shrink()
                : Padding(
                    padding: const EdgeInsets.only(right: 16),
                    child: Center(child: Text('${state.unreadCount} unread')),
                  ),
          ),
        ],
      ),
      body: BlocBuilder<NotificationsCubit, NotificationsState>(
        builder: (context, state) {
          if (state.status == NotificationsStatus.loading && state.items.isEmpty) {
            return const Center(child: CircularProgressIndicator());
          }
          if (state.status == NotificationsStatus.failure && state.items.isEmpty) {
            return Center(child: Text('Error: ${state.error}', style: const TextStyle(color: Colors.red)));
          }
          if (state.items.isEmpty) {
            return const Center(child: Text('No notifications yet'));
          }
          return RefreshIndicator(
            onRefresh: () => context.read<NotificationsCubit>().refresh(),
            child: ListView.builder(
              itemCount: state.items.length,
              itemBuilder: (context, i) {
                final item = state.items[i];
                return ListTile(
                  leading: Icon(
                    item.read ? Icons.notifications_none : Icons.notifications_active,
                    color: item.read ? null : Theme.of(context).colorScheme.primary,
                  ),
                  title: Text(
                    item.title,
                    style: TextStyle(fontWeight: item.read ? FontWeight.normal : FontWeight.bold),
                  ),
                  subtitle: Text(item.body),
                  onTap: item.read ? null : () => context.read<NotificationsCubit>().markRead(item.id),
                );
              },
            ),
          );
        },
      ),
    );
  }
}
