import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';

import 'package:ctxapp/l10n/gen/app_l10n.dart';
import 'package:ctxapp/session/session_cubit.dart';

import '../bloc/auth_cubit.dart';

/// Email/password sign-in and registration screen.
class LoginPage extends StatefulWidget {
  const LoginPage({super.key});

  @override
  State<LoginPage> createState() => _LoginPageState();
}

class _LoginPageState extends State<LoginPage> {
  final TextEditingController _email = TextEditingController();
  final TextEditingController _password = TextEditingController();

  @override
  void dispose() {
    _email.dispose();
    _password.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final l = AppL10n.of(context);
    return Scaffold(
      appBar: AppBar(title: Text(l.authSignInTitle)),
      body: Padding(
        padding: const EdgeInsets.all(16),
        child: BlocConsumer<AuthCubit, AuthState>(
          listener: (context, state) {
            // The form authenticated; hand the session its new status. The gate
            // watches SessionCubit, so this is what swaps the login screen for
            // the app shell.
            if (state.status == AuthStatus.success) {
              context.read<SessionCubit>().signedIn();
            }
          },
          builder: (context, state) {
            final busy = state.status == AuthStatus.submitting;
            final cubit = context.read<AuthCubit>();
            return Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                TextField(
                  controller: _email,
                  keyboardType: TextInputType.emailAddress,
                  decoration: InputDecoration(labelText: l.authEmailLabel),
                ),
                const SizedBox(height: 8),
                TextField(
                  controller: _password,
                  obscureText: true,
                  decoration: InputDecoration(labelText: l.authPasswordLabel),
                ),
                const SizedBox(height: 16),
                FilledButton(
                  onPressed: busy
                      ? null
                      : () => cubit.login(_email.text, _password.text),
                  child: Text(busy ? l.authSigningIn : l.authSignIn),
                ),
                TextButton(
                  onPressed: busy
                      ? null
                      : () => cubit.register(_email.text, _password.text),
                  child: Text(l.authCreateAccount),
                ),
                if (state.status == AuthStatus.failure && state.error != null)
                  Padding(
                    padding: const EdgeInsets.only(top: 8),
                    child: Text(
                      state.error!,
                      style: TextStyle(
                        color: Theme.of(context).colorScheme.error,
                      ),
                    ),
                  ),
              ],
            );
          },
        ),
      ),
    );
  }
}
