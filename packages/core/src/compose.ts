import path from 'node:path';
import fs from 'fs-extra';
import { templateLayout } from './paths.js';
import { loadCatalog, resolveFeatureOrder, type CatalogEntry } from './catalog.js';
import { applyWiring, copyTree, hashTree } from './overlay.js';
import { composeShell, navCapable } from './shell.js';
import { scaffoldFlutterPlatforms } from './flutter.js';
import { writeManifest } from './manifest.js';
import { coreVersion } from './version.js';
import {
  composeAgentsDoc,
  featureDocPath,
  readAgentsFragment,
  renderFeatureDoc,
  FEATURE_DOCS_DIR,
  type AgentsFragment,
} from './agents.js';
import type {
  AppliedFeature,
  FeatureManifest,
  LayoutId,
  Side,
  TemplateVars,
  WiringEdit,
  WorkspaceManifest,
} from './types.js';

export interface CreateOptions {
  /** Absolute path of the workspace root to create (must be empty or absent). */
  targetDir: string;
  vars: TemplateVars;
  /** Toggleable feature ids to enable at create time. */
  features: string[];
  /**
   * The mobile-shell layout structure. Defaults to `bottom_nav` when omitted.
   */
  layout?: LayoutId;
  /**
   * Enabled, nav-capable feature ids to surface as main-navigation tabs, in tab
   * order. Must be a subset of the enabled features. Defaults to every enabled
   * nav-capable feature when omitted. An empty array yields a placeholder shell.
   */
  tabs?: string[];
  /**
   * When true, run `flutter create` to generate the app/ platform scaffolding
   * before the mobile overlay is applied. Requires the Flutter SDK on PATH.
   * Left false by callers (e.g. unit tests) that need a deterministic, offline run.
   */
  scaffoldPlatforms?: boolean;
  /**
   * Version string to stamp into the workspace manifest as `ctx0Version`. A
   * frontend passes its own tool version (e.g. the `ctx0` CLI version) so the
   * manifest records the tool that generated the workspace. Defaults to the
   * `@ctx0/core` engine version when omitted.
   */
  toolVersion?: string;
  /**
   * Explicit template-tree root. Frontends that bundle templates (a published
   * CLI/MCP/portal) pass the path to their bundled `templates/` dir. Omitted in
   * monorepo/dev runs, where the root is auto-detected.
   */
  templatesRoot?: string;
}

export interface CreateResult {
  manifest: WorkspaceManifest;
  /** Aggregated env vars and manual steps to surface to the user. */
  env: string[];
  userSteps: string[];
}

/** The destination prefix inside the workspace for each side. */
function sidePrefix(side: Side): string {
  return side === 'mobile' ? 'app' : 'api';
}

/** Create a full workspace: base + security + requested features, all composed. */
export async function createWorkspace(opts: CreateOptions): Promise<CreateResult> {
  const layout = templateLayout(opts.templatesRoot);
  const { targetDir, vars } = opts;

  await assertEmptyTarget(targetDir);
  await fs.ensureDir(targetDir);

  const catalog = loadCatalog(opts.templatesRoot);
  const order = resolveFeatureOrder(opts.features, catalog);

  const applied: AppliedFeature[] = [];
  const pendingWiring: WiringEdit[] = [];
  const env = new Set<string>();
  const userSteps: string[] = [];
  const agentsFragments: AgentsFragment[] = [];

  const collectManifestExtras = (m: FeatureManifest | undefined) => {
    if (!m) return;
    for (const e of m.env ?? []) env.add(e);
    for (const s of m.userSteps ?? []) userSteps.push(s);
    for (const w of m.wiring ?? []) pendingWiring.push(w);
  };

  // 1. Always-on layers: workspace root, both bases, both security overlays.
  applied.push(await applyLayer('workspace', layout.workspace, targetDir, '', vars));

  // Generate the Flutter platform scaffolding first so the mobile overlay below
  // lays ctx.0's lib/, test/ and pubspec.yaml on top of a runnable Flutter project.
  if (opts.scaffoldPlatforms) {
    await scaffoldFlutterPlatforms(path.join(targetDir, 'app'), vars);
  }

  applied.push(await applyLayer('app_base', layout.mobileBase, targetDir, 'app', vars));
  applied.push(await applyLayer('api_base', layout.apiBase, targetDir, 'api', vars));

  const secMobile = await applyLayer('security_mobile', layout.securityMobile, targetDir, 'app', vars);
  applied.push(secMobile);
  collectManifestExtras(readOptionalManifest(layout.securityMobile));

  const secApi = await applyLayer('security_api', layout.securityApi, targetDir, 'api', vars);
  applied.push(secApi);
  collectManifestExtras(readOptionalManifest(layout.securityApi));

  // 2. Toggleable features, in dependency order, per declared side.
  for (const id of order) {
    const entry = catalog.get(id)!;
    for (const side of entry.manifest.sides) {
      const srcDir = entry.dirs[side];
      if (!srcDir) continue;
      applied.push(await applyLayer(`${id}:${side}`, srcDir, targetDir, sidePrefix(side), vars));
    }
    collectManifestExtras(entry.manifest);
    agentsFragments.push({
      id,
      summary: entry.manifest.summary,
      body: await readFeatureAgents(entry, vars),
    });
  }

  // 3. Apply wiring now that every base/overlay file exists (anchors present).
  await applyWiring(targetDir, pendingWiring, vars);

  // 3b. Generate the mobile navigation shell from the chosen layout + tabs.
  const navLayout: LayoutId = opts.layout ?? 'bottom_nav';
  const tabs = opts.tabs ?? navCapable(catalog, order);
  assertTabsEnabled(tabs, order);
  await composeShell(targetDir, navLayout, tabs, catalog, vars, opts.templatesRoot);

  // 4. Sync the shared wire-protocol spec + golden vectors into the workspace.
  await syncProtocol(targetDir, layout.protocol);

  // 5. Assemble the workspace AGENTS.md from its static preamble plus a generated
  //    table routing to each enabled feature's dedicated doc (in application
  //    order), and write those docs under docs/features/. Both are derived, so
  //    enabling/disabling a feature regenerates them deterministically.
  await composeWorkspaceAgents(targetDir, agentsFragments);
  await writeFeatureDocs(targetDir, agentsFragments);

  const manifest: WorkspaceManifest = {
    schema: 2,
    ctx0Version: opts.toolVersion ?? coreVersion(),
    protocolVersion: readProtocolVersion(layout.protocol),
    vars,
    features: applied,
    navigation: { layout: navLayout, tabs },
  };
  await writeManifest(targetDir, manifest);

  return { manifest, env: [...env], userSteps };
}

/** Copy one layer, returning its manifest record (files + source hash). */
async function applyLayer(
  id: string,
  srcDir: string,
  workspaceRoot: string,
  destPrefix: string,
  vars: TemplateVars,
): Promise<AppliedFeature> {
  if (!(await fs.pathExists(srcDir))) {
    throw new Error(`Template layer "${id}" is missing on disk: ${srcDir}`);
  }
  const files = await copyTree(srcDir, workspaceRoot, destPrefix, vars);
  const hash = await hashTree(srcDir);
  return { id, files, hash };
}

/**
 * Read a feature's `agents.md` fragment from whichever of its side overlays ship
 * one (mobile first, then api), concatenated. A feature usually contributes a
 * single fragment; a two-sided feature may contribute one per side.
 */
async function readFeatureAgents(entry: CatalogEntry, vars: TemplateVars): Promise<string> {
  const parts: string[] = [];
  for (const side of ['mobile', 'api'] as const) {
    const dir = entry.dirs[side];
    if (!dir) continue;
    const fragment = await readAgentsFragment(dir, vars);
    if (fragment) parts.push(fragment);
  }
  return parts.join('\n\n');
}

/**
 * Rewrite the workspace root AGENTS.md as its static preamble plus a generated
 * block documenting the enabled features. The preamble is the AGENTS.md already
 * copied from the workspace template (token-substituted); when the workspace
 * template ships none, the composed block stands on its own.
 */
async function composeWorkspaceAgents(
  workspaceRoot: string,
  fragments: AgentsFragment[],
): Promise<void> {
  const target = path.join(workspaceRoot, 'AGENTS.md');
  const preamble = (await fs.pathExists(target)) ? await fs.readFile(target, 'utf8') : '';
  await fs.writeFile(target, composeAgentsDoc(preamble, fragments), 'utf8');
}

/**
 * Write each enabled feature's dedicated doc under `docs/features/<ID>.md`, and
 * prune any stale docs (from features no longer enabled) so a regeneration on
 * enable/disable stays in sync with the routing table in `AGENTS.md`. The docs are
 * derived artifacts (not tracked in the manifest); this is the sole mechanism that
 * adds and removes them. A no-op prune on a fresh create.
 */
async function writeFeatureDocs(
  workspaceRoot: string,
  fragments: AgentsFragment[],
): Promise<void> {
  const docsDir = path.join(workspaceRoot, FEATURE_DOCS_DIR);
  await fs.ensureDir(docsDir);

  const wanted = new Set(fragments.map((f) => path.basename(featureDocPath(f.id))));
  for (const name of await fs.readdir(docsDir)) {
    if (name.endsWith('.md') && !wanted.has(name)) {
      await fs.remove(path.join(docsDir, name));
    }
  }

  for (const fragment of fragments) {
    const target = path.join(workspaceRoot, featureDocPath(fragment.id));
    await fs.writeFile(target, renderFeatureDoc(fragment), 'utf8');
  }
}

/** Copy the wire-protocol vectors + spec into the workspace's .ctx directory. */
async function syncProtocol(workspaceRoot: string, protocolDir: string): Promise<void> {
  const ctxDir = path.join(workspaceRoot, '.ctx');
  await fs.ensureDir(ctxDir);
  for (const name of ['vectors.json', 'wire-protocol.md']) {
    const src = path.join(protocolDir, name);
    if (await fs.pathExists(src)) {
      await fs.copyFile(src, path.join(ctxDir, name));
    }
  }
}

function readOptionalManifest(dir: string): FeatureManifest | undefined {
  const p = path.join(dir, 'feature.json');
  return fs.existsSync(p) ? (fs.readJsonSync(p) as FeatureManifest) : undefined;
}

function readProtocolVersion(protocolDir: string): string {
  const p = path.join(protocolDir, 'protocol.json');
  if (fs.existsSync(p)) {
    const j = fs.readJsonSync(p) as { version?: string };
    if (j.version) return j.version;
  }
  return '1.0';
}

/** Every requested nav tab must be one of the features actually enabled. */
function assertTabsEnabled(tabs: string[], enabled: string[]): void {
  const enabledSet = new Set(enabled);
  for (const id of tabs) {
    if (!enabledSet.has(id)) {
      throw new Error(
        `Nav tab "${id}" is not an enabled feature. Enable it (or drop it from --tabs).`,
      );
    }
  }
}

async function assertEmptyTarget(dir: string): Promise<void> {
  if (!(await fs.pathExists(dir))) return;
  const entries = await fs.readdir(dir);
  const meaningful = entries.filter((e) => e !== '.git' && e !== '.DS_Store');
  if (meaningful.length > 0) {
    throw new Error(`Target directory is not empty: ${dir}`);
  }
}

export type { CatalogEntry };
