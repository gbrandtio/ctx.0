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
  WorkspaceManifest,
  TemplateVars,
} from './types.js';

// Catalog: discovery + dependency resolution.
export { loadCatalog, resolveFeatureOrder } from './catalog.js';
export type { CatalogEntry } from './catalog.js';

// Navigation shell: layout catalog + nav-capable feature helper.
export { LAYOUTS, isLayoutId, navCapable, composeShell, SHELL_REL } from './shell.js';
export type { LayoutDescriptor } from './shell.js';

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

// Template-tree location + layer/wiring primitives (lower-level, for advanced callers).
export { templateLayout, templatesRoot } from './paths.js';
export type { TemplateLayout } from './paths.js';
export { copyTree, hashTree, applyWiring } from './overlay.js';

// Platform scaffolding adapter (shells out to `flutter create`; opt-in).
export { scaffoldFlutterPlatforms, ensureFlutterAvailable } from './flutter.js';

// Engine version (stamped into the manifest when a frontend does not supply its own).
export { coreVersion } from './version.js';

// Composed AGENTS.md: derive the workspace context doc from per-feature fragments.
export {
  composeAgentsDoc,
  readAgentsFragment,
  AGENTS_BLOCK_START,
  AGENTS_BLOCK_END,
  AGENTS_FRAGMENT_FILE,
} from './agents.js';
export type { AgentsFragment } from './agents.js';
