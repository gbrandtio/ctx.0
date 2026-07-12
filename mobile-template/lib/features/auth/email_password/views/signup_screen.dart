import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/config/app_config.dart';
import '../../../../core/l10n/l10n.dart';
import '../../../../core/widgets/app_button.dart';
import '../../../../core/widgets/app_header.dart';
import '../../../../core/widgets/app_text_field.dart';
import '../bloc/signup_bloc.dart';

class SignupScreen extends StatefulWidget {
  const SignupScreen({super.key});

  @override
  State<SignupScreen> createState() => _SignupScreenState();
}

class _SignupScreenState extends State<SignupScreen> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();

  /// Consent id → granted, seeded from the configured set
  /// (docs/APP_SHELL.md §4).
  late final Map<String, bool> _consents = {
    for (final id in AppConfig.signupConsents.keys) id: false,
  };

  @override
  void dispose() {
    _nameController.dispose();
    _emailController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  String _consentLabel(BuildContext context, String id) {
    return switch (id) {
      'terms_and_privacy' => context.l10n.consentTermsAndPrivacy,
      'marketing_emails' => context.l10n.consentMarketingEmails,
      _ => id,
    };
  }

  void _submit() {
    final l10n = context.l10n;
    if (!(_formKey.currentState?.validate() ?? false)) return;
    final missingRequired = AppConfig.signupConsents.entries
        .any((e) => e.value && !(_consents[e.key] ?? false));
    if (missingRequired) {
      ScaffoldMessenger.of(context)
        ..hideCurrentSnackBar()
        ..showSnackBar(SnackBar(content: Text(l10n.consentRequired)));
      return;
    }
    context.read<SignupBloc>().add(
          SignupSubmitted(
            email: _emailController.text.trim(),
            password: _passwordController.text,
            displayName: _nameController.text.trim().isEmpty
                ? null
                : _nameController.text.trim(),
            consents: Map.of(_consents),
          ),
        );
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    return Scaffold(
      appBar: AppHeader(
        config: HeaderConfig(title: (context) => context.l10n.signupTitle),
      ),
      body: BlocListener<SignupBloc, SignupState>(
        listener: (context, state) {
          if (state is SignupFailure) {
            ScaffoldMessenger.of(context)
              ..hideCurrentSnackBar()
              ..showSnackBar(SnackBar(content: Text(state.message)));
          } else if (state is SignupCodeSent) {
            // Carry the pending registration to the verify screen, which
            // completes the account with the emailed code.
            context.go('/verify-email', extra: state.pending);
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
                    label: l10n.displayNameLabel,
                    controller: _nameController,
                    prefixIcon: Icons.person_outline,
                    textInputAction: TextInputAction.next,
                    autofillHints: const [AutofillHints.name],
                  ),
                  const SizedBox(height: 16),
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
                    autofillHints: const [AutofillHints.newPassword],
                    validator: (value) => (value == null || value.length < 8)
                        ? l10n.passwordTooShort
                        : null,
                  ),
                  const SizedBox(height: 24),
                  for (final id in _consents.keys)
                    CheckboxListTile(
                      value: _consents[id],
                      onChanged: (value) =>
                          setState(() => _consents[id] = value ?? false),
                      title: Text(_consentLabel(context, id)),
                      controlAffinity: ListTileControlAffinity.leading,
                      contentPadding: EdgeInsets.zero,
                    ),
                  const SizedBox(height: 24),
                  BlocBuilder<SignupBloc, SignupState>(
                    builder: (context, state) => AppPrimaryButton(
                      label: l10n.signupButton,
                      loading: state is SignupLoading,
                      onPressed: _submit,
                    ),
                  ),
                  const SizedBox(height: 16),
                  AppSecondaryButton(
                    label: l10n.haveAccountLogin,
                    onPressed: () => context.go('/login'),
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
