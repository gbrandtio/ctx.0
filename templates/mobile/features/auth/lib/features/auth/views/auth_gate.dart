import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import 'package:ctxapp/session/session_cubit.dart';

import 'login_page.dart';

/// Shows the login screen until the session is authenticated, then the app. It
/// renders purely from the app-wide [SessionCubit] — auth owns the login form,
/// the session owns whether anyone is signed in.
class AuthGate extends StatelessWidget {
  const AuthGate({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) {
    return BlocBuilder<SessionCubit, SessionState>(
      builder: (context, state) {
        switch (state.status) {
          case SessionStatus.authenticated:
            return child;
          case SessionStatus.unknown:
            return const Scaffold(body: Center(child: CircularProgressIndicator()));
          case SessionStatus.anonymous:
            return const LoginPage();
        }
      },
    );
  }
}
