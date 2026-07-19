import path from 'node:path';
import fs from 'fs-extra';
import { templateLayout } from './paths.js';
import { loadCatalog, resolveFeatureOrder, type CatalogEntry } from './catalog.js';
import { applyWiring, copyTree, hashTree } from './overlay.js';
import { scaffoldFlutterPlatforms } from './flutter.js';
import { writeManifest } from './manifest.js';
import { coreVersion } from './version.js';
import { composeAgentsDoc, readAgentsFragment, type AgentsFragment } from './agents.js';
import type {
  AppliedFeature,
  FeatureManifest,
  Side,
  TemplateVars,
  WiringEdit,
  WorkspaceManifest,
} from './types.js';

export interface CreateOptions {
  /** Absolute path of the workspace root to create (must be empty or absent). */
  targetDir: string;
  vars: TemplateVars;
  /** Toggleable feature ids to enable at create time (ping is the default). */
  features: string[];
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

  // 4. Sync the shared wire-protocol spec + golden vectors into the workspace.
  await syncProtocol(targetDir, layout.protocol);

  // 5. Assemble the workspace AGENTS.md from its static preamble plus a generated
  //    section per enabled feature (in application order). The file is derived,
  //    so enabling/disabling a feature regenerates the block deterministically.
  await composeWorkspaceAgents(targetDir, agentsFragments);

  const manifest: WorkspaceManifest = {
    schema: 1,
    ctx0Version: opts.toolVersion ?? coreVersion(),
    protocolVersion: readProtocolVersion(layout.protocol),
    vars,
    features: applied,
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

async function assertEmptyTarget(dir: string): Promise<void> {
  if (!(await fs.pathExists(dir))) return;
  const entries = await fs.readdir(dir);
  const meaningful = entries.filter((e) => e !== '.git' && e !== '.DS_Store');
  if (meaningful.length > 0) {
    throw new Error(`Target directory is not empty: ${dir}`);
  }
}

export type { CatalogEntry };
