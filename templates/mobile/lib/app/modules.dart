import '../features/auth/auth_module.dart';
// ctx:ux_onboarding:begin
// ctx:ux_onboarding:end
// ctx:maps_google:begin
// ctx:maps_google:end
// ctx:push_firebase:begin
// ctx:push_firebase:end
// ctx:payments_stripe:begin
// ctx:payments_stripe:end
// ctx:image_capture:begin
// ctx:image_capture:end
// ctx:profile:begin
// ctx:profile:end
// ctx:settings:begin
// ctx:settings:end
// ctx:app_updates:begin
// ctx:app_updates:end
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
  // ctx:ux_onboarding:begin
  // ctx:ux_onboarding:end
  // ctx:maps_google:begin
  // ctx:maps_google:end
  // ctx:profile:begin
  // ctx:profile:end
  // ctx:push_firebase:begin
  // ctx:push_firebase:end
  // ctx:payments_stripe:begin
  // ctx:payments_stripe:end
  // ctx:image_capture:begin
  // ctx:image_capture:end
  // ctx:settings:begin
  // ctx:settings:end
  // ctx:app_updates:begin
  // ctx:app_updates:end
];
