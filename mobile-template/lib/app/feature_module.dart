import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';

/// The plug-n-play contract (docs/APP_SHELL.md §1). Every feature —
/// shipped or product-specific — is a self-contained folder under
/// `lib/features/<feature>/` exposing one FeatureModule, registered with
/// a single line in lib/app/modules.dart.
abstract class FeatureModule {
  const FeatureModule();

  /// GoRouter routes owned by this feature. Blocs are provided per-route
  /// (BlocProvider at the narrowest scope, docs/FLUTTER_ARCHITECTURE.md
  /// §6A). Routes of modules with a [navItem] live inside that module's
  /// bottom-nav branch; other modules' routes are mounted top-level.
  List<RouteBase> get routes;

  /// Repositories this feature contributes; registered once near the root
  /// via MultiRepositoryProvider.
  List<RepositoryProvider> get repositories => const [];

  /// Rare: genuinely app-wide Blocs (AuthBloc, ThemeCubit, LocaleCubit).
  List<BlocProvider>? get globalBlocs => null;

  /// Optional bottom-navigation entry (icon, label, root route).
  NavItem? get navItem => null;

  /// Sections this feature contributes to the settings screen.
  List<SettingsSection> get settingsSections => const [];

  /// Route paths reachable without a session (e.g. /login, /signup). The
  /// router's auth redirect treats everything else as protected.
  List<String> get publicRoutePaths => const [];
}

/// The assembled module list, provided at the app root so shell surfaces
/// (settings screen) can read module contributions without importing
/// lib/app/modules.dart — features depend on the contract, never the
/// registry.
class ModuleRegistry {
  const ModuleRegistry(this.modules);

  final List<FeatureModule> modules;

  /// All settings sections in module order (docs/APP_SHELL.md §3).
  List<SettingsSection> get settingsSections =>
      [for (final module in modules) ...module.settingsSections];
}

/// A bottom-navigation entry contributed by a module. Tab order follows
/// module order in lib/app/modules.dart (docs/APP_SHELL.md §5).
class NavItem {
  const NavItem({
    required this.rootRoute,
    required this.icon,
    this.selectedIcon,
    required this.label,
  });

  /// The branch's root path; must match the module's first route.
  final String rootRoute;

  final IconData icon;
  final IconData? selectedIcon;

  /// Localized label, resolved at build time.
  final String Function(BuildContext context) label;
}

/// A block of the settings screen (docs/APP_SHELL.md §3). Features add
/// sections through their module — never by editing the settings screen.
class SettingsSection {
  const SettingsSection({required this.title, required this.tiles});

  /// Localized section title.
  final String Function(BuildContext context) title;

  /// The section's rows (typically ListTiles).
  final List<Widget> Function(BuildContext context) tiles;
}
