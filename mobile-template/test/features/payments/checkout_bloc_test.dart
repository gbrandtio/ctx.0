import 'package:app_template/core/models/problem_details.dart';
import 'package:app_template/core/result/result.dart';
import 'package:app_template/core/utils/app_exception.dart';
import 'package:app_template/features/payments/bloc/checkout_bloc.dart';
import 'package:app_template/features/payments/data/payments_repository.dart';
import 'package:app_template/features/payments/data/stripe_service.dart';
import 'package:bloc_test/bloc_test.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

class _MockPaymentsRepository extends Mock implements PaymentsRepository {}

void main() {
  late _MockPaymentsRepository repository;

  setUp(() => repository = _MockPaymentsRepository());

  CheckoutBloc build() => CheckoutBloc(repository: repository);

  blocTest<CheckoutBloc, CheckoutState>(
    'emits [processing, success] when the payment completes',
    build: () {
      when(() => repository.pay(orderId: 'o_1')).thenAnswer(
        (_) async => const Result.success(PaymentSheetOutcome.success),
      );
      return build();
    },
    act: (bloc) => bloc.add(const CheckoutSubmitted('o_1')),
    expect: () => const [CheckoutProcessing(), CheckoutSuccess()],
  );

  blocTest<CheckoutBloc, CheckoutState>(
    'user cancellation returns to initial — it is not an error',
    build: () {
      when(() => repository.pay(orderId: 'o_1')).thenAnswer(
        (_) async => const Result.success(PaymentSheetOutcome.canceled),
      );
      return build();
    },
    act: (bloc) => bloc.add(const CheckoutSubmitted('o_1')),
    expect: () => const [CheckoutProcessing(), CheckoutInitial()],
  );

  blocTest<CheckoutBloc, CheckoutState>(
    'emits failure with the client-safe message when the intent is rejected',
    build: () {
      when(() => repository.pay(orderId: 'o_1')).thenAnswer(
        (_) async => const Result.failure(
          AppException(
            ProblemDetails(status: 409, detail: 'Order already paid.'),
          ),
        ),
      );
      return build();
    },
    act: (bloc) => bloc.add(const CheckoutSubmitted('o_1')),
    expect: () => const [
      CheckoutProcessing(),
      CheckoutFailure('Order already paid.'),
    ],
  );

  blocTest<CheckoutBloc, CheckoutState>(
    'double-tap cannot start two payments (droppable)',
    build: () {
      when(() => repository.pay(orderId: 'o_1')).thenAnswer((_) async {
        await Future<void>.delayed(const Duration(milliseconds: 10));
        return const Result.success(PaymentSheetOutcome.success);
      });
      return build();
    },
    act: (bloc) => bloc
      ..add(const CheckoutSubmitted('o_1'))
      ..add(const CheckoutSubmitted('o_1')),
    wait: const Duration(milliseconds: 50),
    verify: (_) => verify(() => repository.pay(orderId: 'o_1')).called(1),
  );
}
