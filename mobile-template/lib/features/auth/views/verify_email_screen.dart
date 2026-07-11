import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

import '../../../core/l10n/l10n.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_header.dart';
import '../../../core/widgets/app_text_field.dart';
import '../bloc/verify_email_cubit.dart';

class VerifyEmailScreen extends StatefulWidget {
  const VerifyEmailScreen({super.key});

  @override
  State<VerifyEmailScreen> createState() => _VerifyEmailScreenState();
}

class _VerifyEmailScreenState extends State<VerifyEmailScreen> {
  final _codeController = TextEditingController();

  @override
  void dispose() {
    _codeController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    return Scaffold(
      appBar: AppHeader(
        config:
            HeaderConfig(title: (context) => context.l10n.verifyEmailTitle),
      ),
      body: BlocListener<VerifyEmailCubit, VerifyEmailState>(
        listener: (context, state) {
          switch (state) {
            case VerifyEmailVerified():
              context.go('/');
            case VerifyEmailResent():
              ScaffoldMessenger.of(context)
                ..hideCurrentSnackBar()
                ..showSnackBar(
                  SnackBar(content: Text(context.l10n.verificationResent)),
                );
            case VerifyEmailFailure(:final message):
              ScaffoldMessenger.of(context)
                ..hideCurrentSnackBar()
                ..showSnackBar(SnackBar(content: Text(message)));
            default:
              break;
          }
        },
        child: SafeArea(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(l10n.verifyEmailBody),
                const SizedBox(height: 24),
                AppTextField(
                  label: l10n.verificationCodeLabel,
                  controller: _codeController,
                  prefixIcon: Icons.pin_outlined,
                  keyboardType: TextInputType.number,
                  textInputAction: TextInputAction.done,
                  autofillHints: const [AutofillHints.oneTimeCode],
                ),
                const SizedBox(height: 32),
                BlocBuilder<VerifyEmailCubit, VerifyEmailState>(
                  builder: (context, state) {
                    final loading = state is VerifyEmailSubmitting;
                    return Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        AppPrimaryButton(
                          label: l10n.verifyButton,
                          loading: loading,
                          onPressed: () => context
                              .read<VerifyEmailCubit>()
                              .verify(_codeController.text.trim()),
                        ),
                        const SizedBox(height: 16),
                        AppSecondaryButton(
                          label: l10n.resendCode,
                          onPressed: loading
                              ? null
                              : () =>
                                  context.read<VerifyEmailCubit>().resend(),
                        ),
                      ],
                    );
                  },
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
