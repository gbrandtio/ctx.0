import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

import '../../../../core/l10n/l10n.dart';
import '../../../../core/widgets/app_button.dart';
import '../../../../core/widgets/app_header.dart';
import '../../../../core/widgets/app_text_field.dart';
import '../bloc/two_factor_cubit.dart';

class TwoFactorScreen extends StatefulWidget {
  const TwoFactorScreen({
    super.key,
    required this.usernameOrEmail,
    required this.password,
  });

  final String usernameOrEmail;
  final String password;

  @override
  State<TwoFactorScreen> createState() => _TwoFactorScreenState();
}

class _TwoFactorScreenState extends State<TwoFactorScreen> {
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
        config: HeaderConfig(title: (context) => 'Two-Factor Authentication'), // TODO: add to l10n
      ),
      body: BlocListener<TwoFactorCubit, TwoFactorState>(
        listener: (context, state) {
          if (state is TwoFactorSuccess) {
            context.go('/');
          } else if (state is TwoFactorFailure) {
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
              children: [
                const Text('Enter the verification code sent to your email.'),
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
                BlocBuilder<TwoFactorCubit, TwoFactorState>(
                  builder: (context, state) {
                    final loading = state is TwoFactorLoading;
                    return AppPrimaryButton(
                      label: l10n.verifyButton,
                      loading: loading,
                      onPressed: () => context
                          .read<TwoFactorCubit>()
                          .submitCode(
                            widget.usernameOrEmail,
                            widget.password,
                            _codeController.text.trim(),
                          ),
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
