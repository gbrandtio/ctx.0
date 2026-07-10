import '../features/auth/auth_module.dart';
import '../features/notifications/notifications_module.dart';
import '../features/profile/profile_module.dart';
import '../features/settings/settings_module.dart';
import 'feature_module.dart';

/// THE plug-n-play point (docs/APP_SHELL.md §1): the single ordered list
/// of feature modules. Adding or removing a capability or business
/// feature = one line here. Tab order follows list order.
const List<FeatureModule> appModules = [
  AuthModule(),
  ProfileModule(),
  NotificationsModule(),
  SettingsModule(),
];
