/**
 * Core types for the ctx.0 composition engine.
 *
 * A generated workspace is produced by copying a `base` template tree and then
 * layering one self-contained *overlay* directory per enabled feature on top.
 * Every overlay (and each `base`) is described by a `feature.json` manifest.
 */

export type Side = 'mobile' | 'api';

/** The main-navigation layout structure chosen for a workspace's mobile shell. */
export type LayoutId = 'bottom_nav' | 'nav_rail' | 'drawer' | 'home_list';

/**
 * Declarative navigation metadata for a feature. A feature that declares `nav`
 * is *nav-capable*: it can be surfaced as a main-navigation tab in the generated
 * app shell. The engine turns this into the shell's destinations/pages/imports.
 */
export interface FeatureNav {
  /** Tab / tile label, e.g. "Secure ping". */
  label: string;
  /** Material icon name (a `Icons.<name>` identifier), e.g. "lock". */
  icon: string;
  /** Entry-screen widget class, e.g. "PingPage". */
  page: string;
  /** App-relative import for the page widget, e.g. "../features/ping/views/ping_page.dart". */
  import: string;
}

/**
 * Declarative metadata for a feature's row in the Settings hub. A feature that
 * declares `settingsEntry` is *settings-capable*: it is surfaced as a `ListTile`
 * inside the generated `SettingsPage` (the `settings` feature) rather than as a
 * main-navigation tab. It is the settings-hub analogue of {@link FeatureNav}.
 */
export interface SettingsEntry {
  /** Row label, e.g. "Language". */
  label: string;
  /** Material icon name (a `Icons.<name>` identifier), e.g. "translate". */
  icon: string;
  /** Destination screen widget class the row opens, e.g. "LanguagePage". */
  page: string;
  /** App-relative import for the page widget, e.g. "../features/l10n/views/language_page.dart". */
  import: string;
}

/** A single idempotent edit to a shared/base file (e.g. Program.cs, pubspec.yaml). */
export interface WiringEdit {
  /** Workspace-relative path of the file to edit, e.g. "api/src/Api/Program.cs". */
  file: string;
  /**
   * Anchor tag to insert near. The target file must contain a line with the
   * marker `// ctx:anchor:<anchor>` (or `# ctx:anchor:<anchor>` / `<!-- -->`).
   * The `insert` text is placed immediately below that marker line.
   */
  anchor: string;
  /** Text to insert. Substitution tokens are applied before insertion. */
  insert: string;
}

/** Dependency additions merged into the target project's manifest. */
export interface FeatureDeps {
  /** pubspec.yaml dependencies to add for the mobile side: name -> version constraint. */
  pubspec?: Record<string, string>;
  /** NuGet PackageReference additions for the api side: package id -> version. */
  nuget?: Record<string, string>;
}

/** A feature manifest (`feature.json`) shipped alongside each overlay. */
export interface FeatureManifest {
  /** Stable feature id, e.g. "auth", "payments_stripe". Matches the overlay dir name. */
  id: string;
  /** Human-readable one-line summary. */
  summary: string;
  /** Which trees this feature touches. */
  sides: Side[];
  /** Feature ids that must be enabled first (resolved transitively). */
  requires?: string[];
  /** True for always-on parts of the workspace (base, security). Cannot be disabled. */
  core?: boolean;
  /**
   * Navigation metadata. Present iff the feature can be surfaced as a
   * main-navigation tab in the generated mobile shell (a *nav-capable* feature).
   */
  nav?: FeatureNav;
  /**
   * Settings-hub metadata. Present iff the feature is surfaced as a row inside
   * the generated `SettingsPage` (a *settings-capable* feature) instead of, or
   * as well as, a main-navigation tab.
   */
  settingsEntry?: SettingsEntry;
  /** Dependency additions per side. */
  deps?: FeatureDeps;
  /** Idempotent edits to shared files. */
  wiring?: WiringEdit[];
  /** Environment variables the consumer must set for this feature to work. */
  env?: string[];
  /** Post-generation manual steps surfaced to the user. */
  userSteps?: string[];
}

/** Record of one applied feature, persisted in the workspace manifest. */
export interface AppliedFeature {
  id: string;
  /** Files this overlay wrote (workspace-relative), for clean removal on disable. */
  files: string[];
  /** Content hash of the overlay source that was applied (integrity / drift check). */
  hash: string;
}

/** The navigation choices persisted for a workspace's mobile shell. */
export interface WorkspaceNavigation {
  /** The main-navigation layout structure of the generated shell. */
  layout: LayoutId;
  /**
   * Enabled, nav-capable feature ids surfaced as main-navigation tabs, in tab
   * order. Empty means the shell renders a minimal placeholder screen.
   */
  tabs: string[];
  /**
   * Enabled, settings-capable feature ids surfaced as rows in the generated
   * `SettingsPage`, in row order. Empty when the `settings` feature is not
   * enabled, or enabled with no settings-capable feature to populate it.
   */
  settings: string[];
}

/** The languages a workspace was generated with. */
export interface WorkspaceLocalization {
  /** Fallback locale; the ARB template and the API's neutral resources. Always "en". */
  default: string;
  /** Enabled locale codes in catalog order, always including the default. */
  locales: string[];
}

/** The colour scheme and typography a workspace was generated with. */
export interface WorkspaceTheme {
  /** Colour-scheme id; its seed is the colour every other colour derives from. */
  scheme: string;
  /** Google Fonts family id, absent when the app uses the platform font. */
  font?: string;
}

/** The `.ctx/manifest.json` persisted at the root of a generated workspace. */
export interface WorkspaceManifest {
  /** Schema version of the manifest itself. */
  schema: 5;
  /** CLI version that generated / last modified the workspace. */
  ctx0Version: string;
  /** Wire-protocol version shared by both sides. */
  protocolVersion: string;
  /** The substitution variables chosen at create time. */
  vars: TemplateVars;
  /** Enabled features in application order (base + security first). */
  features: AppliedFeature[];
  /** The chosen mobile-shell layout and its main-navigation tabs. */
  navigation: WorkspaceNavigation;
  /** The languages the workspace ships translations for. */
  localization: WorkspaceLocalization;
  /** The colour scheme and font chosen for the generated theme. */
  theme: WorkspaceTheme;
}

/** Substitution variables resolved once per workspace. */
export interface TemplateVars {
  /** PascalCase application/type name, e.g. "Acme". Replaces the `CtxApp` token. */
  appName: string;
  /** snake/lower application slug, e.g. "acme". Replaces the `ctxapp` token. */
  appSlug: string;
  /** Reverse-DNS org, e.g. "com.acme". Replaces the `com.ctx.app` org token base. */
  org: string;
  /** Bundle/application id, e.g. "com.acme.app". Replaces `com.ctx.app`. */
  bundleId: string;
}
