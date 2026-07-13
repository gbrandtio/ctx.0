import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
// ctx:auth_email_password:begin
// ctx:auth_email_password:end

import '../../../core/l10n/l10n.dart';
// ctx:auth_2fa_email:begin
import 'package:go_router/go_router.dart';
// ctx:auth_2fa_email:end
// ctx:auth_email_password:begin
// ctx:auth_email_password:end
import '../../../core/widgets/app_header.dart';
// ctx:auth_google:begin
// ctx:auth_google:end
// ctx:auth_email_password:begin
// ctx:auth_email_password:end
import '../bloc/login_bloc.dart';

/// Login shell: each sign-in method is a self-contained widget below,
/// toggled by the scaffolder's `ctx:` markers (docs/INTEGRATIONS.md) —
/// `auth_email_password` and `auth_google`. At least one stays enabled.
class LoginScreen extends StatelessWidget {
  const LoginScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppHeader(
        config: HeaderConfig(
          title: (context) => context.l10n.loginTitle,
          showBackButton: false,
        ),
      ),
      body: BlocListener<LoginBloc, LoginState>(
        listener: (context, state) {
          // One-shot side effects only (docs/STATE_MANAGEMENT.md §5);
          // successful navigation is the router redirect's job.
          if (state is LoginFailure) {
            ScaffoldMessenger.of(context)
              ..hideCurrentSnackBar()
              ..showSnackBar(SnackBar(content: Text(state.message)));
          }
// ctx:auth_2fa_email:begin
          if (state is LoginRequiresTwoFactor) {
            context.go('/2fa', extra: {
              'usernameOrEmail': state.usernameOrEmail,
              'password': state.password,
            });
          }
// ctx:auth_2fa_email:end
        },
        child: SafeArea(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: const [
                // ctx:auth_email_password:begin
                // ctx:auth_email_password:end
                // ctx:auth_google:begin
                // ctx:auth_google:end
              ],
            ),
          ),
        ),
      ),
    );
  }
}

// ctx:auth_email_password:begin
// ctx:auth_email_password:end

// ctx:auth_google:begin
// ctx:auth_google:end
