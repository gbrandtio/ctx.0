import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

import '../../app/feature_module.dart';
import '../../core/l10n/l10n.dart';
import '../../data/repositories/auth_repository.dart';
import 'bloc/auth_bloc.dart';
import 'bloc/login_bloc.dart';
// ctx:auth_email_password:begin
import 'email_password/bloc/signup_bloc.dart';
import 'email_password/bloc/verify_email_cubit.dart';
import 'email_password/data/pending_registration.dart';
import 'email_password/views/signup_screen.dart';
import 'email_password/views/verify_email_screen.dart';
// ctx:auth_email_password:end
// ctx:auth_google:begin
import 'google/google_auth_service.dart';
// ctx:auth_google:end
import 'views/login_screen.dart';

/// Shipped auth module (docs/APP_SHELL.md): the permanent auth core —
/// login shell, global AuthBloc, session lifecycle, logout — plus the
/// scaffoldable sign-in methods (docs/INTEGRATIONS.md): email/password
/// with mandatory verification (`auth_email_password`) and Google
/// Sign-In (`auth_google`). At least one method must stay enabled.
/// Screen Blocs are provided per-route — the narrowest scope
/// (docs/FLUTTER_ARCHITECTURE.md §6A).
class AuthModule extends FeatureModule {
  const AuthModule();

  @override
  List<RouteBase> get routes => [
        GoRoute(
          path: '/login',
          builder: (context, state) => BlocProvider(
            create: (context) => LoginBloc(
              authRepository: context.read<AuthRepository>(),
              // ctx:auth_google:begin
              googleAuth: context.read<GoogleAuthService>(),
              // ctx:auth_google:end
            ),
            child: const LoginScreen(),
          ),
        ),
        // ctx:auth_email_password:begin
        GoRoute(
          path: '/signup',
          builder: (context, state) => BlocProvider(
            create: (context) => SignupBloc(
              authRepository: context.read<AuthRepository>(),
            ),
            child: const SignupScreen(),
          ),
        ),
        GoRoute(
          path: '/verify-email',
          // Reached only from the signup screen, which passes the pending
          // registration as `extra`; a direct hit falls back to signup.
          redirect: (context, state) =>
              state.extra is PendingRegistration ? null : '/signup',
          builder: (context, state) => BlocProvider(
            create: (context) => VerifyEmailCubit(
              authRepository: context.read<AuthRepository>(),
              pending: state.extra as PendingRegistration,
            ),
            child: const VerifyEmailScreen(),
          ),
        ),
        // ctx:auth_email_password:end
      ];

  @override
  List<RepositoryProvider> get repositories => [
        // ctx:auth_google:begin
        RepositoryProvider<GoogleAuthService>(
          create: (_) => GoogleAuthService(),
        ),
        // ctx:auth_google:end
      ];

  @override
  List<BlocProvider>? get globalBlocs => [
        BlocProvider<AuthBloc>(
          lazy: false,
          create: (context) => AuthBloc(
            authRepository: context.read<AuthRepository>(),
          )..add(const AuthSubscriptionRequested()),
        ),
      ];

  /// Session controls live with the auth core (not the profile module) so
  /// logout survives any combination of scaffolded features.
  @override
  List<SettingsSection> get settingsSections => [
        SettingsSection(
          title: (context) => context.l10n.settingsAccountSection,
          tiles: (context) => [
            ListTile(
              leading: const Icon(Icons.logout),
              title: Text(context.l10n.logout),
              onTap: () =>
                  context.read<AuthBloc>().add(const AuthLogoutRequested()),
            ),
          ],
        ),
      ];

  @override
  List<String> get publicRoutePaths => const [
        '/login',
        // ctx:auth_email_password:begin
        '/signup',
        // ctx:auth_email_password:end
      ];
}
