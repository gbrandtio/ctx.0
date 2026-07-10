import 'package:app_template/core/result/result.dart';
import 'package:app_template/data/services/api/interceptors/caching_client.dart';
import 'package:app_template/features/payments/data/payment_api_service.dart';
import 'package:app_template/features/payments/data/payments_repository.dart';
import 'package:app_template/features/payments/data/stripe_service.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:mocktail/mocktail.dart';

class _MockApi extends Mock implements PaymentApiService {}

class _MockStripe extends Mock implements StripeService {}

class _MockCachingClient extends Mock implements CachingClient {}

void main() {
  late _MockApi api;
  late _MockStripe stripe;
  late _MockCachingClient cachingClient;
  late PaymentsRepository repository;

  setUp(() {
    api = _MockApi();
    stripe = _MockStripe();
    cachingClient = _MockCachingClient();
    when(() => cachingClient.invalidatePattern(any()))
        .thenAnswer((_) async {});
    repository = PaymentsRepository(
      api: api,
      stripe: stripe,
      cachingClient: cachingClient,
    );
  });

  test('success invalidates every order-dependent cache pattern', () async {
    when(() => api.createPaymentIntent(orderId: 'o_1'))
        .thenAnswer((_) async => 'secret');
    when(() => stripe.presentPaymentSheet(
          clientSecret: 'secret',
          merchantDisplayName: any(named: 'merchantDisplayName'),
        )).thenAnswer((_) async => PaymentSheetOutcome.success);

    final result = await repository.pay(orderId: 'o_1');

    expect(result, isA<Success<PaymentSheetOutcome>>());
    for (final pattern in PaymentsRepository.invalidatedPatterns) {
      verify(() => cachingClient.invalidatePattern(pattern)).called(1);
    }
  });

  test('cancellation leaves the cache untouched (nothing changed)',
      () async {
    when(() => api.createPaymentIntent(orderId: 'o_1'))
        .thenAnswer((_) async => 'secret');
    when(() => stripe.presentPaymentSheet(
          clientSecret: 'secret',
          merchantDisplayName: any(named: 'merchantDisplayName'),
        )).thenAnswer((_) async => PaymentSheetOutcome.canceled);

    await repository.pay(orderId: 'o_1');

    verifyNever(() => cachingClient.invalidatePattern(any()));
  });

  test('the client never sends an amount — only the order ID', () async {
    when(() => api.createPaymentIntent(orderId: 'o_1'))
        .thenAnswer((_) async => 'secret');
    when(() => stripe.presentPaymentSheet(
          clientSecret: any(named: 'clientSecret'),
          merchantDisplayName: any(named: 'merchantDisplayName'),
        )).thenAnswer((_) async => PaymentSheetOutcome.success);

    await repository.pay(orderId: 'o_1');

    verify(() => api.createPaymentIntent(orderId: 'o_1')).called(1);
  });
}
