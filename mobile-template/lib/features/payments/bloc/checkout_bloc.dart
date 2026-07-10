import 'package:bloc/bloc.dart';
import 'package:bloc_concurrency/bloc_concurrency.dart';
import 'package:equatable/equatable.dart';

import '../../../core/result/result.dart';
import '../../../core/utils/app_exception.dart';
import '../data/payments_repository.dart';
import '../data/stripe_service.dart';

part 'checkout_event.dart';
part 'checkout_state.dart';

/// Checkout screen Bloc. Submission is droppable — a double-tap can never
/// open two payment sheets or create two intents client-side (the server
/// also enforces idempotency via `payment-intent:{orderId}`).
class CheckoutBloc extends Bloc<CheckoutEvent, CheckoutState> {
  CheckoutBloc({required PaymentsRepository repository})
      : _repository = repository,
        super(const CheckoutInitial()) {
    on<CheckoutSubmitted>(_onSubmitted, transformer: droppable());
  }

  final PaymentsRepository _repository;

  Future<void> _onSubmitted(
    CheckoutSubmitted event,
    Emitter<CheckoutState> emit,
  ) async {
    emit(const CheckoutProcessing());
    final result = await _repository.pay(orderId: event.orderId);
    switch (result) {
      case Success(:final value):
        emit(value == PaymentSheetOutcome.success
            ? const CheckoutSuccess()
            : const CheckoutInitial()); // canceled: back to idle
      case Failure(:final error):
        emit(CheckoutFailure(AppException.from(error).userFriendlyMessage));
    }
  }
}
