import path from 'node:path';
import fs from 'fs-extra';
import pc from 'picocolors';
import prompts from 'prompts';
import {
  createWorkspace,
  resolveVars,
  loadCatalog,
  navCapable,
  isLayoutId,
  LAYOUTS,
  type CatalogEntry,
  type LayoutId,
} from '@ctx0/core';
import { cliVersion } from '../version.js';

export interface CreateArgs {
  name: string;
  org?: string;
  dir?: string;
  /** Feature ids to enable (non-interactive path). */
  features?: string[];
  /** Layout id to use (non-interactive path). */
  layout?: string;
  /** Feature ids to surface as main-navigation tabs (non-interactive path). */
  tabs?: string[];
  /** Generate the Flutter platform scaffolding via `flutter create` (default true). */
  platforms?: boolean;
}

/** The setup choices, resolved either interactively or from flags. */
interface Setup {
  features: string[];
  layout: LayoutId;
  /** Undefined means "let the engine default to every nav-capable feature". */
  tabs: string[] | undefined;
}

export async function runCreate(args: CreateArgs): Promise<void> {
  const vars = resolveVars(args.name, args.org);
  const targetDir = path.resolve(args.dir ?? process.cwd(), vars.appSlug);

  console.log(pc.bold(`\nCreating ctx.0 workspace ${pc.cyan(vars.appName)}`));
  console.log(`  location : ${pc.dim(targetDir)}`);
  console.log(`  bundle   : ${pc.dim(vars.bundleId)}`);

  const catalog = loadCatalog();
  const setup = shouldPrompt(args) ? await promptSetup(catalog) : resolveFromFlags(args);

  const tabsForDisplay = setup.tabs ?? navCapable(catalog, setup.features);
  console.log(`  features : ${pc.dim(setup.features.join(', ') || '(none)')}`);
  console.log(`  layout   : ${pc.dim(setup.layout)}`);
  console.log(`  tabs     : ${pc.dim(tabsForDisplay.join(', ') || '(none)')}\n`);

  const scaffoldPlatforms = args.platforms !== false;
  if (scaffoldPlatforms) {
    console.log(pc.dim('  Running `flutter create` for the app/ platform scaffolding…'));
  }

  const result = await createWorkspace({
    targetDir,
    vars,
    features: setup.features,
    layout: setup.layout,
    tabs: setup.tabs,
    scaffoldPlatforms,
    toolVersion: cliVersion(),
  });

  console.log(pc.green('✓ Workspace generated.'));
  console.log(`  app/  Flutter (Bloc)   api/  .NET (Clean Architecture)`);
  console.log(`  ${result.manifest.features.length} layers, protocol v${result.manifest.protocolVersion}\n`);

  if (result.env.length) {
    console.log(pc.bold('Environment variables to set:'));
    for (const e of result.env) console.log(`  - ${e}`);
    console.log();
  }
  if (result.userSteps.length) {
    console.log(pc.bold('Next steps:'));
    for (const s of result.userSteps) console.log(`  - ${s}`);
    console.log();
  }

  console.log(pc.dim(`cd ${path.relative(process.cwd(), targetDir)} && cat README.md`));
  await ensureReadmeHint(targetDir);
}

/** Prompt only when the user drives no setup flags and we have an interactive TTY. */
function shouldPrompt(args: CreateArgs): boolean {
  const hasFlags = Boolean(args.features?.length || args.layout || args.tabs?.length);
  return !hasFlags && Boolean(process.stdin.isTTY && process.stdout.isTTY);
}

/** The three-step guided flow: layout → features → main-nav tabs. */
async function promptSetup(catalog: Map<string, CatalogEntry>): Promise<Setup> {
  const onCancel = () => {
    throw new Error('Cancelled — no workspace was created.');
  };

  // 1. Layout structure.
  const { layout } = await prompts(
    {
      type: 'select',
      name: 'layout',
      message: 'Choose the app layout structure',
      choices: LAYOUTS.map((l) => ({ title: l.label, description: l.description, value: l.id })),
      initial: 0,
    },
    { onCancel },
  );

  // 2. Which features to enable (nothing pre-selected — pick any number).
  const { features } = await prompts(
    {
      type: 'multiselect',
      name: 'features',
      message: 'Select features to enable (choose any number)',
      choices: [...catalog.entries()].map(([id, entry]) => ({
        title: id,
        description: entry.manifest.summary,
        value: id,
        selected: false,
      })),
      hint: 'space to toggle · enter to confirm',
      instructions: MULTISELECT_INSTRUCTIONS,
    },
    { onCancel },
  );

  const selected: string[] = features ?? [];
  reportAutoDeps(catalog, selected);

  const layoutId = layout as LayoutId;

  // Partition the enabled features: those that can be main-navigation tabs
  // (declare a `nav` block) versus those that integrate another way (e.g. auth,
  // which wraps the app rather than owning a tab).
  const navFeatures = navCapable(catalog, selected);
  const nonNavFeatures = selected.filter((id) => !navFeatures.includes(id));

  // 3. Which nav-capable features appear in the main navigation (all pre-checked).
  let tabs: string[] = navFeatures;
  if (navFeatures.length > 0) {
    const answer = await prompts(
      {
        type: 'multiselect',
        name: 'tabs',
        message: `Which features appear in the ${layoutLabel(layoutId)}?`,
        choices: navFeatures.map((id) => ({
          title: catalog.get(id)!.manifest.nav!.label,
          value: id,
          selected: true,
        })),
        hint: 'space to toggle · enter to confirm',
        instructions: MULTISELECT_INSTRUCTIONS,
      },
      { onCancel },
    );
    tabs = answer.tabs ?? [];
  }

  // 4. Always-on features: enabled but not navigation tabs. Surface them so the
  // user sees where every enabled feature ends up, rather than dropping them.
  reportAlwaysOnFeatures(catalog, nonNavFeatures);

  return { features: selected, layout: layoutId, tabs };
}

/**
 * Custom `prompts` multiselect footer so the space-to-toggle interaction is
 * always visible (the default terse hint made the list feel single-select).
 */
const MULTISELECT_INSTRUCTIONS =
  '\n  ↑/↓ move · space toggle · a select all · enter confirm';

/** Human-readable label for a layout id, from the shared LAYOUTS descriptors. */
function layoutLabel(layout: LayoutId): string {
  return LAYOUTS.find((l) => l.id === layout)?.label ?? 'main navigation';
}

/**
 * Print a distinct step listing enabled features that are not navigation tabs,
 * with a one-line note on how each integrates. These features have no tab to
 * toggle, so this is informational rather than a picker.
 */
function reportAlwaysOnFeatures(catalog: Map<string, CatalogEntry>, ids: string[]): void {
  if (ids.length === 0) return;
  console.log(pc.bold('\nAlways-on features (not navigation tabs):'));
  for (const id of ids) {
    const summary = catalog.get(id)?.manifest.summary ?? '';
    console.log(`  - ${pc.cyan(id)} — ${summary} ${pc.dim('(integrates app-wide, not a tab)')}`);
  }
  console.log();
}

/** Print a note when selecting a feature auto-enables its dependencies. */
function reportAutoDeps(catalog: Map<string, CatalogEntry>, selected: string[]): void {
  const chosen = new Set(selected);
  const added = new Set<string>();
  for (const id of selected) {
    for (const dep of catalog.get(id)?.manifest.requires ?? []) {
      if (!chosen.has(dep)) added.add(dep);
    }
  }
  if (added.size > 0) {
    console.log(pc.dim(`  (auto-enabling required dependencies: ${[...added].join(', ')})`));
  }
}

/** Non-interactive resolution from flags, with sensible defaults. */
function resolveFromFlags(args: CreateArgs): Setup {
  const layout = args.layout ?? 'bottom_nav';
  if (!isLayoutId(layout)) {
    throw new Error(
      `Unknown --layout "${layout}". Choose one of: ${LAYOUTS.map((l) => l.id).join(', ')}.`,
    );
  }
  return {
    features: args.features ?? [],
    layout,
    tabs: args.tabs, // undefined → engine defaults to every nav-capable feature
  };
}

async function ensureReadmeHint(dir: string): Promise<void> {
  // Non-fatal: only warn if the workspace template forgot a README.
  if (!(await fs.pathExists(path.join(dir, 'README.md')))) {
    console.log(pc.yellow('  (note: generated workspace has no README.md)'));
  }
}
