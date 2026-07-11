import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

import '../../app/feature_module.dart';
import '../../data/repositories/auth_repository.dart';
import 'bloc/auth_bloc.dart';
import 'bloc/login_bloc.dart';
import 'bloc/signup_bloc.dart';
import 'bloc/verify_email_cubit.dart';
import 'data/google_auth_service.dart';
import 'data/pending_registration.dart';
import 'views/login_screen.dart';
import 'views/signup_screen.dart';
import 'views/verify_email_screen.dart';

/// Shipped auth module (docs/APP_SHELL.md): login, signup, email
/// verification, Google Sign-In. Owns the global AuthBloc. Screen Blocs
/// are provided per-route — the narrowest scope
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
              googleAuth: context.read<GoogleAuthService>(),
            ),
            child: const LoginScreen(),
          ),
        ),
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
      ];

  @override
  List<RepositoryProvider> get repositories => [
        RepositoryProvider<GoogleAuthService>(
          create: (_) => GoogleAuthService(),
        ),
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

  @override
  List<String> get publicRoutePaths => const ['/login', '/signup'];
}
