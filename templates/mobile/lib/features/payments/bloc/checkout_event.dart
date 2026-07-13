part of 'checkout_bloc.dart';

sealed class CheckoutEvent {
  const CheckoutEvent();
}

final class CheckoutSubmitted extends CheckoutEvent {
  const CheckoutSubmitted(this.orderId);

  /// The server-issued order to pay (amount is authoritative server-side).
  final String orderId;
}
