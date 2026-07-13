import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';

import '../../app/feature_module.dart';

/// App Updates Feature
/// (docs/INTEGRATIONS.md)
///
/// Contains the logic for intercepting API requests and checking the
/// minimum client version against the server. If the server returns a 426
/// Upgrade Required, an overlay is triggered to block the UI.
class AppUpdatesModule extends FeatureModule {
  const AppUpdatesModule();

  @override
  List<RouteBase> get routes => const [];

  @override
  Future<void> init() async {}
}

/// Global notifier triggered when the API returns a 426 Upgrade Required.
final updateRequiredNotifier = ValueNotifier<bool>(false);
