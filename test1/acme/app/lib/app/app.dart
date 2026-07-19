import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import 'di.dart';
import 'home_page.dart';
// ctx:anchor:app-imports
import '../features/auth/views/auth_gate.dart';

/// Root widget for Acme. Feature Blocs are provided via [ctxAppProviders]
/// (extended by feature overlays through the `ctx:anchor:providers` marker).
class AcmeRoot extends StatelessWidget {
  const AcmeRoot({super.key});

  @override
  Widget build(BuildContext context) {
    return MultiBlocProvider(
      providers: ctxAppProviders(),
      child: MaterialApp(
        title: 'Acme',
        theme: ThemeData(colorSchemeSeed: Colors.indigo, useMaterial3: true),
        home: _home(),
      ),
    );
  }

  /// The root screen. Feature overlays may wrap it (e.g. an auth gate) by
  /// inserting below the `home-wrap` anchor.
  Widget _home() {
    Widget home = const CtxHomePage();
    // ctx:anchor:home-wrap
    home = AuthGate(child: home);
    return home;
  }
}
