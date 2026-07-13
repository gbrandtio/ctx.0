import 'dart:async';

import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../data/repositories/auth_repository.dart';
// ctx:ux_onboarding:begin
// ctx:ux_onboarding:end
import 'feature_module.dart';
import 'shell_scaffold.dart';

/// Assembles the GoRouter from module registrations plus the global auth
/// redirect (docs/APP_SHELL.md §1): modules with a NavItem become bottom-
/// nav branches; the rest mount top-level. Signed-out users land on
/// /login with the attempted deep link preserved in ?from=.
GoRouter buildRouter({
  required List<FeatureModule> modules,
  required AuthRepository authRepository,
  // ctx:ux_onboarding:begin
  // ctx:ux_onboarding:end
}) {
  final navModules = modules.where((m) => m.navItem != null).toList();
  final otherModules = modules.where((m) => m.navItem == null).toList();
  final publicPaths = {
    for (final module in modules) ...module.publicRoutePaths,
  };
  final homePath = navModules.isEmpty
      ? '/splash'
      : navModules.first.navItem!.rootRoute;

  return GoRouter(
    initialLocation: '/splash',
    refreshListenable: _AuthStateNotifier(authRepository),
    redirect: (context, state) {
      final auth = authRepository.currentState;
      final path = state.uri.path;
      final isPublic = publicPaths.any(
        (p) => path == p || path.startsWith('$p/'),
      );

      return switch (auth) {
        // Session restore in flight: hold on the splash screen.
        AuthUnknown() => path == '/splash' ? null : '/splash',
        Unauthenticated() =>
          // ctx:ux_onboarding:begin
          // ctx:ux_onboarding:end
          isPublic
              ? null
              : Uri(
                  path: '/login',
                  queryParameters: path == '/splash'
                      ? null
                      : {'from': state.uri.toString()},
                ).toString(),
        Authenticated() =>
          // ctx:ux_onboarding:begin
          // ctx:ux_onboarding:end
          (isPublic || path == '/splash')
              ? (state.uri.queryParameters['from'] ?? homePath)
              : null,
      };
    },
    routes: [
      // Well-known home alias so features never hardcode another module's
      // root route.
      GoRoute(path: '/', redirect: (context, state) => homePath),
      GoRoute(
        path: '/splash',
        builder: (context, state) =>
            const Scaffold(body: Center(child: CircularProgressIndicator())),
      ),
      if (navModules.isNotEmpty)
        StatefulShellRoute.indexedStack(
          builder: (context, state, shell) => ShellScaffold(
            shell: shell,
            items: [for (final m in navModules) m.navItem!],
          ),
          branches: [
            for (final module in navModules)
              StatefulShellBranch(routes: module.routes),
          ],
        ),
      for (final module in otherModules) ...module.routes,
    ],
  );
}

/// Bridges AuthRepository.authStateChanges to the router's
/// refreshListenable so redirects re-evaluate on every auth transition.
class _AuthStateNotifier extends ChangeNotifier {
  _AuthStateNotifier(AuthRepository repository) {
    _subscription = repository.authStateChanges.listen(
      (_) => notifyListeners(),
    );
  }

  late final StreamSubscription<AuthState> _subscription;

  @override
  void dispose() {
    _subscription.cancel();
    super.dispose();
  }
}
