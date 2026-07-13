import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
// ctx:auth_email_password:begin
import 'package:go_router/go_router.dart';
// ctx:auth_email_password:end

import '../../../core/l10n/l10n.dart';
// ctx:auth_email_password:begin
import '../../../core/widgets/app_button.dart';
// ctx:auth_email_password:end
import '../../../core/widgets/app_header.dart';
// ctx:auth_google:begin
import '../../../core/widgets/app_icons.dart';
// ctx:auth_google:end
// ctx:auth_email_password:begin
import '../../../core/widgets/app_text_field.dart';
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
        },
        child: SafeArea(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: const [
                // ctx:auth_email_password:begin
                _EmailPasswordLoginForm(),
                SizedBox(height: 16),
                // ctx:auth_email_password:end
                // ctx:auth_google:begin
                _GoogleSignInButton(),
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
class _EmailPasswordLoginForm extends StatefulWidget {
  const _EmailPasswordLoginForm();

  @override
  State<_EmailPasswordLoginForm> createState() =>
      _EmailPasswordLoginFormState();
}

class _EmailPasswordLoginFormState extends State<_EmailPasswordLoginForm> {
  final _formKey = GlobalKey<FormState>();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  void _submit() {
    if (_formKey.currentState?.validate() ?? false) {
      context.read<LoginBloc>().add(
        LoginSubmitted(_emailController.text.trim(), _passwordController.text),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    return Form(
      key: _formKey,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          AppTextField(
            label: l10n.emailLabel,
            controller: _emailController,
            prefixIcon: Icons.email_outlined,
            keyboardType: TextInputType.emailAddress,
            textInputAction: TextInputAction.next,
            autofillHints: const [AutofillHints.email],
            validator: (value) => (value == null || !value.contains('@'))
                ? l10n.emailInvalid
                : null,
          ),
          const SizedBox(height: 16),
          AppTextField(
            label: l10n.passwordLabel,
            controller: _passwordController,
            prefixIcon: Icons.lock_outline,
            obscureText: true,
            textInputAction: TextInputAction.done,
            autofillHints: const [AutofillHints.password],
            onSubmitted: (_) => _submit(),
            validator: (value) =>
                (value == null || value.isEmpty) ? l10n.passwordRequired : null,
          ),
          const SizedBox(height: 32),
          BlocBuilder<LoginBloc, LoginState>(
            builder: (context, state) => AppPrimaryButton(
              label: l10n.loginButton,
              loading: state is LoginLoading,
              onPressed: _submit,
            ),
          ),
          const SizedBox(height: 24),
          AppSecondaryButton(
            label: l10n.noAccountSignUp,
            onPressed: () => context.go('/signup'),
          ),
        ],
      ),
    );
  }
}
// ctx:auth_email_password:end

// ctx:auth_google:begin
class _GoogleSignInButton extends StatelessWidget {
  const _GoogleSignInButton();

  @override
  Widget build(BuildContext context) {
    return BlocBuilder<LoginBloc, LoginState>(
      builder: (context, state) => OutlinedButton.icon(
        onPressed: state is LoginLoading
            ? null
            : () =>
                  context.read<LoginBloc>().add(const LoginWithGooglePressed()),
        icon: const AppIcon(AppIcons.googleLogo, size: 20),
        label: Text(context.l10n.signInWithGoogle),
      ),
    );
  }
}

// ctx:auth_google:end
