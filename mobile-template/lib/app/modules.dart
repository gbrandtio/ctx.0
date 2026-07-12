import '../features/auth/auth_module.dart';
// ctx:maps_google:begin
// ctx:off import '../features/maps/maps_module.dart';
// ctx:maps_google:end
// ctx:push_firebase:begin
// ctx:off import '../features/notifications/notifications_module.dart';
// ctx:push_firebase:end
// ctx:payments_stripe:begin
// ctx:off import '../features/payments/payments_module.dart';
// ctx:payments_stripe:end
import '../features/profile/profile_module.dart';
import '../features/settings/settings_module.dart';
import 'feature_module.dart';

/// THE plug-n-play point (docs/APP_SHELL.md §1): the single ordered list
/// of feature modules. Adding or removing a business feature = one line
/// here. Tab order follows list order.
///
/// The `ctx:` marker blocks belong to the integration scaffolder — enable
/// or disable an optional integration ONLY via
/// `dart run tool/scaffold.dart` (docs/INTEGRATIONS.md), never by hand.
const List<FeatureModule> appModules = [
  AuthModule(),
  // ctx:maps_google:begin
  // ctx:off MapsModule(),
  // ctx:maps_google:end
  ProfileModule(),
  // ctx:push_firebase:begin
  // ctx:off NotificationsModule(),
  // ctx:push_firebase:end
  // ctx:payments_stripe:begin
  // ctx:off PaymentsModule(),
  // ctx:payments_stripe:end
  SettingsModule(),
];
