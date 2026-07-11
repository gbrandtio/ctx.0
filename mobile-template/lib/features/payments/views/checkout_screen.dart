import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

import '../../../core/l10n/l10n.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_header.dart';
import '../bloc/checkout_bloc.dart';

/// Generic checkout for a server-issued order. Business features navigate
/// here with the order they created (`context.push('/checkout/<orderId>')`)
/// and receive `true` back when the payment succeeded.
class CheckoutScreen extends StatelessWidget {
  const CheckoutScreen({super.key, required this.orderId});

  final String orderId;

  @override
  Widget build(BuildContext context) {
    final l10n = context.l10n;
    return Scaffold(
      appBar: AppHeader(
        config: HeaderConfig(title: (context) => context.l10n.checkoutTitle),
      ),
      body: BlocListener<CheckoutBloc, CheckoutState>(
        listener: (context, state) {
          switch (state) {
            case CheckoutSuccess():
              ScaffoldMessenger.of(context)
                ..hideCurrentSnackBar()
                ..showSnackBar(
                  SnackBar(content: Text(context.l10n.paymentSuccessful)),
                );
              context.pop(true);
            case CheckoutFailure(:final message):
              ScaffoldMessenger.of(context)
                ..hideCurrentSnackBar()
                ..showSnackBar(SnackBar(content: Text(message)));
            default:
              break;
          }
        },
        child: SafeArea(
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Card(
                  child: ListTile(
                    leading: const Icon(Icons.receipt_long_outlined),
                    title: Text(l10n.orderReference),
                    subtitle: Text(orderId),
                  ),
                ),
                const Spacer(),
                Text(
                  l10n.checkoutExplanation,
                  style: Theme.of(context).textTheme.bodySmall,
                  textAlign: TextAlign.center,
                ),
                const SizedBox(height: 16),
                BlocBuilder<CheckoutBloc, CheckoutState>(
                  builder: (context, state) => AppPrimaryButton(
                    label: l10n.payNow,
                    loading: state is CheckoutProcessing,
                    onPressed: () => context
                        .read<CheckoutBloc>()
                        .add(CheckoutSubmitted(orderId)),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
