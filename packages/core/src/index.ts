/**
 * `@ctx0/core` — the ctx.0 composition engine.
 *
 * This is the CLI-free heart of the scaffolder: it discovers the feature
 * catalog, resolves dependency order, composes a workspace from `base` template
 * trees plus feature overlays, applies idempotent wiring, and records a
 * reversible manifest. It returns structured results and never prints — so the
 * `ctx0` CLI, the future ctx.0-MCP server, and the ctx.0-portal can each sit
 * over the same engine as a thin adapter.
 */

// Types (the shared vocabulary: manifests, wiring, vars).
export type {
  Side,
  LayoutId,
  FeatureNav,
  WiringEdit,
  FeatureDeps,
  FeatureManifest,
  AppliedFeature,
  WorkspaceNavigation,
  WorkspaceLocalization,
  WorkspaceTheme,
  WorkspaceManifest,
  TemplateVars,
} from './types.js';

// Catalog: discovery + dependency resolution.
export { loadCatalog, resolveFeatureOrder } from './catalog.js';
export type { CatalogEntry } from './catalog.js';

// Navigation shell: layout catalog + nav-capable feature helper.
export { LAYOUTS, isLayoutId, navCapable, composeShell, SHELL_REL } from './shell.js';
export type { LayoutDescriptor } from './shell.js';

// Localization: the offered languages + the per-locale translation composer.
export {
  LOCALES,
  DEFAULT_LOCALE,
  isLocaleCode,
  resolveLocales,
  composeLocales,
  ARB_DIR_REL,
  L10N_SUPPORT_REL,
  L10N_FRAGMENT_DIR,
  RESOURCES_DIR_REL,
  LOCALIZATION_DIR_REL,
  SUPPORTED_CULTURES_REL,
} from './l10n.js';
export type { LocaleDescriptor, LocaleSource } from './l10n.js';

// Theme: the offered colour schemes and fonts + the generated theme library.
export {
  COLOR_SCHEMES,
  DEFAULT_SCHEME,
  FONTS,
  GOOGLE_FONTS_DEPENDENCY,
  isSchemeId,
  isFontId,
  findScheme,
  findFont,
  resolveTheme,
  uncoveredLocales,
  googleFontsMethod,
  composeTheme,
  THEME_REL,
} from './theme.js';
export type { ColorSchemeDescriptor, FontDescriptor, ThemeChoice } from './theme.js';

// Composition: the top-level create operation and its I/O shapes.
export { createWorkspace } from './compose.js';
export type { CreateOptions, CreateResult } from './compose.js';

// Workspace manifest (state) read/write helpers.
export { readManifest, writeManifest, isWorkspace, MANIFEST_REL } from './manifest.js';

// Substitution primitives (also useful to frontends deriving vars from input).
export {
  resolveVars,
  substitute,
  slugify,
  pascalCase,
  isProbablyBinary,
  TOKENS,
} from './substitute.js';

// Deterministic ordering (the comparator every derived list is sorted with).
export { compareUtf8, sortUtf8 } from './order.js';

// Server secret generation (shared by every frontend so the encodings match).
export { generateServerSecrets } from './secrets.js';
export type { ServerSecrets } from './secrets.js';

// Template-tree location + layer/wiring primitives (lower-level, for advanced callers).
export { templateLayout, templatesRoot } from './paths.js';
export type { TemplateLayout } from './paths.js';
export { copyTree, hashTree, applyWiring } from './overlay.js';

// Platform scaffolding adapter (shells out to `flutter create`; opt-in).
export { scaffoldFlutterPlatforms, ensureFlutterAvailable } from './flutter.js';

// Versions: the engine's own (stamped into the manifest when a frontend does not
// supply its own) and the wire-protocol version of a generated workspace.
export { coreVersion, protocolVersion } from './version.js';

// Composed AGENTS.md: derive the workspace context doc from per-feature fragments.
export {
  composeAgentsDoc,
  readAgentsFragment,
  AGENTS_BLOCK_START,
  AGENTS_BLOCK_END,
  AGENTS_FRAGMENT_FILE,
} from './agents.js';
export type { AgentsFragment } from './agents.js';
