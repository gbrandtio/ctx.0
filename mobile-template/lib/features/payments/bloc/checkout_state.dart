part of 'checkout_bloc.dart';

sealed class CheckoutState extends Equatable {
  const CheckoutState();

  @override
  List<Object?> get props => [];
}

final class CheckoutInitial extends CheckoutState {
  const CheckoutInitial();
}

final class CheckoutProcessing extends CheckoutState {
  const CheckoutProcessing();
}

final class CheckoutSuccess extends CheckoutState {
  const CheckoutSuccess();
}

final class CheckoutFailure extends CheckoutState {
  const CheckoutFailure(this.message);
  final String message;

  @override
  List<Object?> get props => [message];
}
