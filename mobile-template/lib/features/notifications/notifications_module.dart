import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';
import 'package:http/http.dart' as http;

import '../../app/feature_module.dart';
import '../../core/l10n/l10n.dart';
import '../../data/repositories/auth_repository.dart';
import 'bloc/notifications_bloc.dart';
import 'data/notification_api_service.dart';
import 'data/notifications_repository.dart';
import 'data/push_token_service.dart';
import 'views/notifications_screen.dart';

/// Shipped notifications module: FCM registration bound to the auth
/// lifecycle + the in-app feed
/// (api-template/docs/features/NOTIFICATIONS.md).
class NotificationsModule extends FeatureModule {
  const NotificationsModule();

  @override
  List<RouteBase> get routes => [
        GoRoute(
          path: '/notifications',
          builder: (context, state) => BlocProvider(
            create: (context) => NotificationsBloc(
              repository: context.read<NotificationsRepository>(),
            )..add(const NotificationsFetched()),
            child: const NotificationsScreen(),
          ),
        ),
      ];

  @override
  List<RepositoryProvider> get repositories => [
        // lazy: false — registration must follow auth state from app
        // start, not from the first screen visit.
        RepositoryProvider<NotificationsRepository>(
          lazy: false,
          create: (context) => NotificationsRepository(
            api: NotificationApiService(context.read<http.Client>()),
            pushTokens: PushTokenService(),
            authRepository: context.read<AuthRepository>(),
          ),
        ),
      ];

  @override
  NavItem? get navItem => NavItem(
        rootRoute: '/notifications',
        icon: Icons.notifications_outlined,
        selectedIcon: Icons.notifications,
        label: (context) => context.l10n.notificationsTitle,
      );
}
