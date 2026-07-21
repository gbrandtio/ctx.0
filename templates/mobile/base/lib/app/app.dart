import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import 'di.dart';
import 'shell.dart';
import 'theme.dart';
// ctx:anchor:app-imports

/// Root widget for CtxApp. Feature Blocs are provided via [ctxAppProviders]
/// (extended by feature overlays through the `ctx:anchor:providers` marker).
class CtxAppRoot extends StatelessWidget {
  const CtxAppRoot({super.key});

  @override
  Widget build(BuildContext context) {
    return MultiBlocProvider(
      providers: ctxAppProviders(),
      child: const _CtxMaterialApp(),
    );
  }
}

/// The [MaterialApp] itself. It is a separate widget so that its build context
/// sits *below* [ctxAppProviders], letting feature overlays wired into the
/// `app-material` anchor read app-wide Blocs — the locale Cubit, for instance —
/// while configuring the app.
class _CtxMaterialApp extends StatelessWidget {
  const _CtxMaterialApp();

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'CtxApp',
      theme: AppTheme.light(),
      darkTheme: AppTheme.dark(),
      // ctx:anchor:app-material
      builder: _overlay,
      home: _home(),
    );
  }

  /// Wraps every route, inside the [MaterialApp] but above whatever the route
  /// renders. Feature overlays that must be visible regardless of the screen —
  /// a consent banner shown before sign-in, for instance — insert below the
  /// `app-overlay` anchor.
  Widget _overlay(BuildContext context, Widget? child) {
    Widget content = child ?? const SizedBox.shrink();
    // ctx:anchor:app-overlay
    return content;
  }

  /// The root screen: the generated navigation shell. Feature overlays may wrap
  /// it (e.g. an auth gate) by inserting below the `home-wrap` anchor.
  Widget _home() {
    Widget home = const CtxShell();
    // ctx:anchor:home-wrap
    return home;
  }
}
