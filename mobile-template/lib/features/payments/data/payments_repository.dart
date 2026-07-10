import '../../../core/config/app_config.dart';
import '../../../core/result/result.dart';
import '../../../data/services/api/interceptors/caching_client.dart';
import 'payment_api_service.dart';
import 'stripe_service.dart';

/// Orchestrates the order-based checkout
/// (api-template/docs/features/PAYMENTS_STRIPE.md §2): server-created
/// PaymentIntent → PaymentSheet → cache invalidation of everything a
/// successful payment affects (docs/CACHING_IMPLEMENTATION.md
/// "Event-Driven Invalidation").
class PaymentsRepository {
  PaymentsRepository({
    required PaymentApiService api,
    required StripeService stripe,
    required CachingClient cachingClient,
  })  : _api = api,
        _stripe = stripe,
        _cachingClient = cachingClient;

  final PaymentApiService _api;
  final StripeService _stripe;
  final CachingClient _cachingClient;

  /// Cached URL patterns a successful payment invalidates. Extend this
  /// list when business features add order-dependent endpoints (the
  /// CACHING_IMPLEMENTATION.md rule: every mutation documents its
  /// invalidations).
  static const invalidatedPatterns = ['/orders', '/users/me'];

  Future<Result<PaymentSheetOutcome>> pay({required String orderId}) async {
    try {
      final clientSecret = await _api.createPaymentIntent(orderId: orderId);
      final outcome = await _stripe.presentPaymentSheet(
        clientSecret: clientSecret,
        merchantDisplayName: AppConfig.appName,
      );
      if (outcome == PaymentSheetOutcome.success) {
        for (final pattern in invalidatedPatterns) {
          await _cachingClient.invalidatePattern(pattern);
        }
      }
      return Result.success(outcome);
    } on Exception catch (e) {
      return Result.failure(e);
    }
  }
}
