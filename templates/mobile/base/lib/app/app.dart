import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import 'di.dart';
import 'shell.dart';
// ctx:anchor:app-imports

/// Root widget for CtxApp. Feature Blocs are provided via [ctxAppProviders]
/// (extended by feature overlays through the `ctx:anchor:providers` marker).
class CtxAppRoot extends StatelessWidget {
  const CtxAppRoot({super.key});

  @override
  Widget build(BuildContext context) {
    return MultiBlocProvider(
      providers: ctxAppProviders(),
      child: MaterialApp(
        title: 'CtxApp',
        theme: ThemeData(colorSchemeSeed: Colors.indigo, useMaterial3: true),
        home: _home(),
      ),
    );
  }

  /// The root screen: the generated navigation shell. Feature overlays may wrap
  /// it (e.g. an auth gate) by inserting below the `home-wrap` anchor.
  Widget _home() {
    Widget home = const CtxShell();
    // ctx:anchor:home-wrap
    return home;
  }
}
