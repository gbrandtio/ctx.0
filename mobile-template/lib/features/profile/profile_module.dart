import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

import '../../app/feature_module.dart';
import '../../core/l10n/l10n.dart';
import '../../data/repositories/auth_repository.dart';
import '../auth/bloc/auth_bloc.dart';
import 'bloc/profile_cubit.dart';
import 'views/edit_profile_screen.dart';
import 'views/profile_screen.dart';

/// Shipped profile module: view/edit profile plus the Account settings
/// section (docs/APP_SHELL.md §3).
class ProfileModule extends FeatureModule {
  const ProfileModule();

  @override
  List<RouteBase> get routes => [
        GoRoute(
          path: '/profile',
          builder: (context, state) => BlocProvider(
            create: (context) => ProfileCubit(
              authRepository: context.read<AuthRepository>(),
            ),
            child: const ProfileScreen(),
          ),
          routes: [
            GoRoute(
              path: 'edit',
              builder: (context, state) => BlocProvider(
                create: (context) => ProfileCubit(
                  authRepository: context.read<AuthRepository>(),
                ),
                child: const EditProfileScreen(),
              ),
            ),
          ],
        ),
      ];

  @override
  NavItem? get navItem => NavItem(
        rootRoute: '/profile',
        icon: Icons.person_outline,
        selectedIcon: Icons.person,
        label: (context) => context.l10n.profileTitle,
      );

  @override
  List<SettingsSection> get settingsSections => [
        SettingsSection(
          title: (context) => context.l10n.settingsAccountSection,
          tiles: (context) => [
            ListTile(
              leading: const Icon(Icons.edit_outlined),
              title: Text(context.l10n.editProfile),
              onTap: () => context.go('/profile/edit'),
            ),
            ListTile(
              leading: const Icon(Icons.logout),
              title: Text(context.l10n.logout),
              onTap: () =>
                  context.read<AuthBloc>().add(const AuthLogoutRequested()),
            ),
          ],
        ),
      ];
}
