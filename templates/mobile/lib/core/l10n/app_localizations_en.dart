// ignore: unused_import
import 'package:intl/intl.dart' as intl;
import 'app_localizations.dart';

// ignore_for_file: type=lint

/// The translations for English (`en`).
class AppLocalizationsEn extends AppLocalizations {
  AppLocalizationsEn([String locale = 'en']) : super(locale);

  @override
  String get loginTitle => 'Log in';

  @override
  String get emailLabel => 'Email';

  @override
  String get passwordLabel => 'Password';

  @override
  String get loginButton => 'Log in';

  @override
  String get signInWithGoogle => 'Sign in with Google';

  @override
  String get noAccountSignUp => 'No account? Sign up';

  @override
  String get emailInvalid => 'Enter a valid email address';

  @override
  String get passwordRequired => 'Enter your password';

  @override
  String get signupTitle => 'Create account';

  @override
  String get displayNameLabel => 'Name';

  @override
  String get signupButton => 'Sign up';

  @override
  String get haveAccountLogin => 'Already have an account? Log in';

  @override
  String get passwordTooShort => 'Password must be at least 8 characters';

  @override
  String get consentTermsAndPrivacy =>
      'I agree to the Terms of Service and Privacy Policy';

  @override
  String get consentMarketingEmails => 'Send me product updates by email';

  @override
  String get consentRequired => 'Please accept the required terms to continue';

  @override
  String get verifyEmailTitle => 'Verify email';

  @override
  String get verifyEmailBody =>
      'We sent a verification code to your email address. Enter it below to verify your account.';

  @override
  String get verificationCodeLabel => 'Verification code';

  @override
  String get verifyButton => 'Verify';

  @override
  String get resendCode => 'Resend code';

  @override
  String get verificationResent => 'Verification email sent';

  @override
  String get profileTitle => 'Profile';

  @override
  String get editProfile => 'Edit profile';

  @override
  String get saveButton => 'Save';

  @override
  String get profileUpdated => 'Profile updated';

  @override
  String get logout => 'Log out';

  @override
  String get mapTitle => 'Map';

  @override
  String get mapRefresh => 'Refresh nearby items';

  @override
  String get locationUnavailable =>
      'Location is unavailable. Enable location services to see items near you.';

  @override
  String get checkoutTitle => 'Checkout';

  @override
  String get orderReference => 'Order';

  @override
  String get payNow => 'Pay now';

  @override
  String get paymentSuccessful => 'Payment successful';

  @override
  String get checkoutExplanation =>
      'You will be charged the amount of the order shown above. Payment is processed securely by Stripe.';

  @override
  String get notificationsTitle => 'Notifications';

  @override
  String get notificationsEmpty => 'No notifications yet';

  @override
  String get settingsTitle => 'Settings';

  @override
  String get settingsAccountSection => 'Account';

  @override
  String get settingsPersonalisationSection => 'Personalisation';

  @override
  String get settingsPrivacySection => 'Privacy';

  @override
  String get themeLabel => 'Theme';

  @override
  String get themeSystem => 'System';

  @override
  String get themeLight => 'Light';

  @override
  String get themeDark => 'Dark';

  @override
  String get languageLabel => 'Language';

  @override
  String get languageSystem => 'System';

  @override
  String get exportMyData => 'Export my data';

  @override
  String get exportRequested =>
      'Export requested — you will be notified when it is ready';

  @override
  String get privacyPolicy => 'Privacy policy';

  @override
  String get termsOfService => 'Terms of service';

  @override
  String get deleteAccount => 'Delete account';

  @override
  String get deleteAccountConfirmTitle => 'Delete account?';

  @override
  String get deleteAccountConfirmBody =>
      'This permanently deletes your account and removes your personal data. This action cannot be undone.';

  @override
  String get cancel => 'Cancel';

  @override
  String get deleteConfirm => 'Delete';

  @override
  String get trackingConsentTitle => 'Data Usage & Analytics';

  @override
  String get trackingConsentSubtitle =>
      'Help us improve the app by sharing anonymous usage data.';

  @override
  String get gdprBannerTitle => 'We value your privacy';

  @override
  String get gdprBannerMessage =>
      'We use anonymous data to improve your experience. You can always change this in Settings.';

  @override
  String get gdprWarningMessage =>
      'Some features are disabled to protect your privacy.';

  @override
  String get accept => 'Accept';

  @override
  String get decline => 'Decline';
}
