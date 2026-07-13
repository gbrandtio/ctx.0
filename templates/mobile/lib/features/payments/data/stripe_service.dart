import 'package:flutter/foundation.dart';
import 'package:flutter_stripe/flutter_stripe.dart';

import '../../../core/config/app_config.dart';

/// Outcome of presenting the payment sheet. Cancellation is a normal
/// user action, not an error.
enum PaymentSheetOutcome { success, canceled }

/// Thin wrapper over the Stripe SDK so Blocs stay testable and PCI scope
/// stays delegated: card data never touches our code or API
/// (templates/api/docs/features/PAYMENTS_STRIPE.md §1). Google Pay and
/// Apple Pay ride through the same PaymentSheet.
class StripeService {
  bool _initialized = false;

  Future<void> _ensureInitialized() async {
    if (_initialized) return;
    Stripe.publishableKey = AppConfig.stripePublishableKey;
    if (AppConfig.applePayMerchantId.isNotEmpty) {
      Stripe.merchantIdentifier = AppConfig.applePayMerchantId;
    }
    await Stripe.instance.applySettings();
    _initialized = true;
  }

  /// Presents the payment sheet for a server-created PaymentIntent. The
  /// client only ever handles the clientSecret — amounts are authoritative
  /// server-side.
  Future<PaymentSheetOutcome> presentPaymentSheet({
    required String clientSecret,
    required String merchantDisplayName,
  }) async {
    await _ensureInitialized();
    await Stripe.instance.initPaymentSheet(
      paymentSheetParameters: SetupPaymentSheetParameters(
        paymentIntentClientSecret: clientSecret,
        merchantDisplayName: merchantDisplayName,
        googlePay: PaymentSheetGooglePay(
          merchantCountryCode: AppConfig.merchantCountryCode,
          testEnv: !kReleaseMode,
        ),
        applePay: AppConfig.applePayMerchantId.isEmpty
            ? null
            : PaymentSheetApplePay(
                merchantCountryCode: AppConfig.merchantCountryCode,
              ),
      ),
    );
    try {
      await Stripe.instance.presentPaymentSheet();
      return PaymentSheetOutcome.success;
    } on StripeException catch (e) {
      if (e.error.code == FailureCode.Canceled) {
        return PaymentSheetOutcome.canceled;
      }
      rethrow;
    }
  }
}
