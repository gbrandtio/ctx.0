/// App-level configuration and feature toggles — a shell configuration
/// point (docs/APP_SHELL.md §5). Environment-specific values come from
/// --dart-define (docs/ENVIRONMENT_VARIABLES.md); product-specific
/// constants (policy URLs, consent set) are edited here directly.
abstract final class AppConfig {
  static const String appName =
      String.fromEnvironment('APP_NAME', defaultValue: 'App Template');

  // ---- GDPR / legal surface (docs/APP_SHELL.md §4) ----
  static const String privacyPolicyUrl = String.fromEnvironment(
    'PRIVACY_POLICY_URL',
    defaultValue: 'https://example.com/privacy',
  );
  static const String termsOfServiceUrl = String.fromEnvironment(
    'TERMS_OF_SERVICE_URL',
    defaultValue: 'https://example.com/terms',
  );

  /// Consent toggles collected at signup (docs/features/SIGNUP.md).
  /// Key = consent id sent to the API; value = whether it is required.
  static const Map<String, bool> signupConsents = {
    'terms_and_privacy': true, // required
    'marketing_emails': false, // optional
  };

  // ---- Payments (docs/ENVIRONMENT_VARIABLES.md; payments module) ----
  /// Publishable key only — the secret key lives exclusively on the API.
  static const String stripePublishableKey =
      String.fromEnvironment('STRIPE_PUBLISHABLE_KEY');

  /// Apple Pay merchant identifier (empty disables Apple Pay).
  static const String applePayMerchantId =
      String.fromEnvironment('APPLE_PAY_MERCHANT_ID');

  /// ISO country code for Google Pay / Apple Pay.
  static const String merchantCountryCode =
      String.fromEnvironment('MERCHANT_COUNTRY_CODE', defaultValue: 'US');
}
