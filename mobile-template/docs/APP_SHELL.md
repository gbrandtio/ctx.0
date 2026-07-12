# App Shell & Feature Modules

The template ships a configurable **application shell** — navigation, headers, settings, and the GDPR surface — plus a plug-n-play **module system**. Product features plug into the shell through explicit registration points; the shell itself is never rewritten per product.

## 1. The `FeatureModule` Contract

Every feature (shipped or product-specific) is a self-contained folder under `lib/features/<feature>/` that exposes one `FeatureModule`:

```dart
abstract class FeatureModule {
  /// One-time bootstrap for SDKs this module owns (e.g. Firebase for the
  /// notifications module). Called by main() after the security plane is
  /// up, before runApp. Must degrade gracefully (log, don't throw) when
  /// platform config is missing. Keeps main.dart vendor-free
  /// (INTEGRATIONS.md §4).
  Future<void> init() async {}

  /// GoRouter routes owned by this feature. Blocs are provided per-route
  /// (BlocProvider at the narrowest scope, per FLUTTER_ARCHITECTURE.md §6A).
  List<RouteBase> get routes;

  /// Repositories this feature contributes; registered once near the root
  /// via MultiRepositoryProvider.
  List<RepositoryProvider> get repositories;

  /// Rare: genuinely app-wide Blocs (AuthBloc, ThemeCubit, LocaleCubit).
  List<BlocProvider>? get globalBlocs;

  /// Optional bottom-navigation entry (icon, label, root route).
  NavItem? get navItem;

  /// Sections this feature contributes to the settings screen.
  List<SettingsSection> get settingsSections;

  /// Route paths reachable without a session (e.g. /login, /signup).
  /// Everything else is protected by the auth redirect.
  List<String> get publicRoutePaths;
}
```

`lib/app/modules.dart` holds the single ordered `List<FeatureModule>`. **Adding or removing a business feature = one line there.** Exception: the optional vendor integrations (maps, push, payments) are registered inside `ctx:` marker blocks managed exclusively by the scaffolder — never toggle those lines by hand (`docs/INTEGRATIONS.md`). The shell composes everything else:

- **Router** (`lib/app/router.dart`): GoRouter assembled from every module's `routes`, plus a global auth redirect driven by `AuthRepository.authStateChanges` (signed-out users land on login; deep links are preserved).
- **DI**: `MultiRepositoryProvider` from all `repositories`; `globalBlocs` mounted at the app root.
- **Bottom navigation** (`lib/app/shell_scaffold.dart`): built from the registered `navItem`s in module order. A module with a `navItem` gets its routes mounted inside its own tab branch (its navigation stack survives tab switches); modules without one mount top-level.
- **Settings screen**: composed from all `settingsSections` (read via the root-provided `ModuleRegistry`) — a feature never edits the settings screen directly.

### Adding a business feature (the whole recipe)

1. Spec it: copy `../../docs/features/FEATURE_SPEC_TEMPLATE.md` → `../../docs/features/<FEATURE>.md` and fill it in.
2. Create `lib/features/<feature>/` with `bloc/`, `data/`, `views/` (per `FLUTTER_ARCHITECTURE.md` §5) and a `<feature>_module.dart` implementing `FeatureModule`.
3. Register it: one line in `lib/app/modules.dart`.

No shell file other than `modules.dart` is touched.

## 2. Headers (App Bars)

Headers are **configured, not hand-built**. Each route declares a `HeaderConfig` (title key, actions, visibility, transparency); the shell's `AppHeader` widget renders it from theme tokens (`docs/UI_UX_GUIDELINES.md`). Custom per-screen app bars are forbidden — extend `HeaderConfig` instead so styling stays centralized.

## 3. Settings

The settings screen is an ordered composition of `SettingsSection`s. Shipped sections:

| Section | Contributed by | Contents |
|---|---|---|
| Account | auth module (permanent) | logout — session controls stay available whatever features are scaffolded away |
| Profile | profile module (`profile`) | edit profile |
| Personalisation | settings module (`settings`) | theme (light/dark), language |
| Privacy (GDPR) | settings module (`settings`) | delete account, export my data, consent & policy links |

A product feature adds its own section via `settingsSections` — never by editing the settings screen.

## 4. GDPR Surface

Shipped, wired to the API's capabilities:

- **Delete account**: confirmation flow → API's anonymizing delete → full local purge (cache, secure storage, prefs) → logout.
- **Export my data**: requests a server-side export (`user_exports` capability); delivery is notified via push.
- **Consent & policies**: privacy policy / terms links (configured in `AppConfig`) and consent toggles collected at signup (see `../../docs/features/SIGNUP.md`).

## 5. Configuration Points

| What | Where |
|---|---|
| Environment (API base URL, flags) | `lib/core/config/` via `--dart-define` (see `ENVIRONMENT_VARIABLES.md`) |
| Brand colors, fonts, dark/light themes | `lib/core/theme/` per `UI_UX_GUIDELINES.md`; assets in `docs/brand-kit/` |
| Languages | ARB files in `lib/core/l10n/` per `FLUTTER_LOCALIZATION.md` |
| Nav items, tab order | module order in `lib/app/modules.dart` |
| Policy URLs, consent set | `AppConfig` |

Shipped modules (auth, profile, settings, notifications, payments, maps) are optional beyond auth/profile/settings: removing one is deleting its line in `modules.dart`.
