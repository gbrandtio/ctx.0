import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

import '../../../core/l10n/l10n.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_header.dart';
import '../../../core/widgets/app_icons.dart';
import '../../../core/widgets/app_text_field.dart';
import '../bloc/login_bloc.dart';

class LoginScreen extends StatefulWidget {
  const LoginScreen({super.key});

  @override
  State<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends State<LoginScreen> {
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
            LoginSubmitted(
              _emailController.text.trim(),
              _passwordController.text,
            ),
          );
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
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
            child: Form(
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
                    validator: (value) =>
                        (value == null || !value.contains('@'))
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
                    validator: (value) => (value == null || value.isEmpty)
                        ? l10n.passwordRequired
                        : null,
                  ),
                  const SizedBox(height: 32),
                  BlocBuilder<LoginBloc, LoginState>(
                    builder: (context, state) {
                      final loading = state is LoginLoading;
                      return Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          AppPrimaryButton(
                            label: l10n.loginButton,
                            loading: loading,
                            onPressed: _submit,
                          ),
                          const SizedBox(height: 16),
                          OutlinedButton.icon(
                            onPressed: loading
                                ? null
                                : () => context
                                    .read<LoginBloc>()
                                    .add(const LoginWithGooglePressed()),
                            icon: const AppIcon(AppIcons.googleLogo, size: 20),
                            label: Text(l10n.signInWithGoogle),
                          ),
                        ],
                      );
                    },
                  ),
                  const SizedBox(height: 24),
                  AppSecondaryButton(
                    label: l10n.noAccountSignUp,
                    onPressed: () => context.go('/signup'),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
