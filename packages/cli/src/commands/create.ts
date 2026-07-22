import path from 'node:path';
import fs from 'fs-extra';
import pc from 'picocolors';
import prompts from 'prompts';
import type {
  CatalogFeature,
  ColorSchemeDescriptor,
  FontDescriptor,
  LayoutDescriptor,
  LayoutId,
  LocaleDescriptor,
} from '@ctx0/engine-server/contract';
import { withEngine, type Engine } from '../engine.js';
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
  /** Language codes to ship (non-interactive path). */
  locales?: string[];
  /** Colour-scheme id to theme the app with (non-interactive path). */
  scheme?: string;
  /** Google Fonts family id to use (non-interactive path). */
  font?: string;
  /** Generate the Flutter platform scaffolding via `flutter create` (default true). */
  platforms?: boolean;
}

/** The setup choices, resolved either interactively or from flags. */
interface Setup {
  features: string[];
  layout: LayoutId;
  /** Undefined means "let the engine default to every nav-capable feature". */
  tabs: string[] | undefined;
  /** Undefined means "let the engine ship every offered language". */
  locales: string[] | undefined;
  /** Undefined means "let the engine use its default colour scheme". */
  scheme: string | undefined;
  /** Undefined means the platform font, with no font package added. */
  font: string | undefined;
}

export async function runCreate(args: CreateArgs): Promise<void> {
  await withEngine(async (engine) => {
    const { vars } = await engine.call('vars.resolve', { name: args.name, org: args.org });
    const targetDir = path.resolve(args.dir ?? process.cwd(), vars.appSlug);

    console.log(pc.bold(`\nCreating ctx.0 workspace ${pc.cyan(vars.appName)}`));
    console.log(`  location : ${pc.dim(targetDir)}`);
    console.log(`  bundle   : ${pc.dim(vars.bundleId)}`);

    const { features: catalog } = await engine.call('catalog.list', {});
    const { layouts } = await engine.call('layouts.list', {});
    const { locales, default: defaultLocale } = await engine.call('locales.list', {});
    const theme = await engine.call('theme.list', {});
    const setup = shouldPrompt(args)
      ? await promptSetup(engine, catalog, layouts, locales, defaultLocale, theme)
      : resolveFromFlags(args, layouts, theme);

    // The engine decides which features can be tabs, so ask it rather than
    // second-guessing the catalog here.
    const resolved = await engine.call('catalog.resolve', { features: setup.features });
    console.log(`  features : ${pc.dim(setup.features.join(', ') || '(none)')}`);
    console.log(`  layout   : ${pc.dim(setup.layout)}`);
    console.log(
      `  languages: ${pc.dim((setup.locales ?? locales.map((l) => l.code)).join(', '))}`,
    );
    console.log(`  tabs     : ${pc.dim((setup.tabs ?? resolved.navCapable).join(', ') || '(none)')}`);
    console.log(`  scheme   : ${pc.dim(setup.scheme ?? theme.defaultScheme)}`);
    console.log(`  font     : ${pc.dim(fontLabel(theme.fonts, setup.font))}`);

    // Said once, for either path: the picker annotates coverage while choosing,
    // but a font can also arrive by flag, and either way it is worth repeating
    // next to the languages it does not cover.
    const chosenFont = setup.font ? theme.fonts.find((f) => f.id === setup.font) : undefined;
    if (chosenFont) {
      warnCoverage(chosenFont, setup.locales ?? locales.map((l) => l.code), locales);
    }
    console.log();

    const scaffoldPlatforms = args.platforms !== false;
    if (scaffoldPlatforms) {
      console.log(pc.dim('  Running `flutter create` for the app/ platform scaffolding…'));
    }

    const result = await engine.call('workspace.create', {
      targetDir,
      name: args.name,
      org: args.org,
      features: setup.features,
      layout: setup.layout,
      tabs: setup.tabs,
      locales: setup.locales,
      scheme: setup.scheme,
      font: setup.font,
      scaffoldPlatforms,
      toolVersion: cliVersion(),
    });

    console.log(pc.green('✓ Workspace generated.'));
    console.log(`  app/  Flutter (Bloc)   api/  .NET (Clean Architecture)`);
    console.log(
      `  ${result.manifest.features.length} layers, protocol v${result.manifest.protocolVersion}\n`,
    );

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
  });
}

/** Prompt only when the user drives no setup flags and we have an interactive TTY. */
function shouldPrompt(args: CreateArgs): boolean {
  const hasFlags = Boolean(
    args.features?.length ||
      args.layout ||
      args.tabs?.length ||
      args.locales?.length ||
      args.scheme ||
      args.font,
  );
  return !hasFlags && Boolean(process.stdin.isTTY && process.stdout.isTTY);
}

/** The colour schemes and fonts the engine offers, as `theme.list` reports them. */
interface ThemeCatalog {
  schemes: ColorSchemeDescriptor[];
  fonts: FontDescriptor[];
  defaultScheme: string;
}

/**
 * The guided flow: layout → languages → colour scheme → font → features →
 * main-nav tabs. The theme steps sit after the languages so the font step knows
 * which languages the app ships and can say which of them a family cannot draw.
 */
async function promptSetup(
  engine: Engine,
  catalog: CatalogFeature[],
  layouts: LayoutDescriptor[],
  locales: LocaleDescriptor[],
  defaultLocale: string,
  theme: ThemeCatalog,
): Promise<Setup> {
  const onCancel = () => {
    throw new Error('Cancelled — no workspace was created.');
  };

  // 1. Layout structure.
  const { layout } = await prompts(
    {
      type: 'select',
      name: 'layout',
      message: 'Choose the app layout structure',
      choices: layouts.map((l) => ({ title: l.label, description: l.description, value: l.id })),
      initial: 0,
    },
    { onCancel },
  );

  // 2. Which languages the app ships. The default language is the fallback for
  // both sides, so it is always included rather than offered as a choice.
  const optional = locales.filter((l) => l.code !== defaultLocale);
  const { extraLocales } = await prompts(
    {
      type: 'multiselect',
      name: 'extraLocales',
      message: `Select the languages to ship (${languageLabel(locales, defaultLocale)} is always included)`,
      choices: optional.map((locale) => ({
        title: `${locale.englishLabel} — ${locale.label}`,
        description: locale.code,
        value: locale.code,
        selected: true,
      })),
      hint: 'space to toggle · enter to confirm',
      instructions: MULTISELECT_INSTRUCTIONS,
    },
    { onCancel },
  );
  const chosenLocales = [defaultLocale, ...((extraLocales as string[] | undefined) ?? [])];

  // 3. The colour scheme. Its seed is the colour every other colour in the app
  // is derived from, so this is the whole of the app's palette.
  const { scheme } = await prompts(
    {
      type: 'select',
      name: 'scheme',
      message: 'Choose a colour scheme (optional; enter keeps the default)',
      choices: theme.schemes.map((s) => ({
        title: s.label,
        description: s.description,
        value: s.id,
      })),
      initial: Math.max(
        0,
        theme.schemes.findIndex((s) => s.id === theme.defaultScheme),
      ),
    },
    { onCancel },
  );

  // 4. The font, annotated with the languages each family cannot draw.
  const font = await promptFont(theme.fonts, chosenLocales, locales, onCancel);

  // 5. Which features to enable (nothing pre-selected — pick any number).
  const { features } = await prompts(
    {
      type: 'multiselect',
      name: 'features',
      message: 'Select features to enable (choose any number)',
      choices: catalog.map((feature) => ({
        title: feature.id,
        description: feature.summary,
        value: feature.id,
        selected: false,
      })),
      hint: 'space to toggle · enter to confirm',
      instructions: MULTISELECT_INSTRUCTIONS,
    },
    { onCancel },
  );

  const selected: string[] = features ?? [];
  const layoutId = layout as LayoutId;

  // Ask the engine what this selection actually expands to: the dependencies it
  // will add, and which of the enabled features can be navigation tabs.
  const resolved = await engine.call('catalog.resolve', { features: selected });
  reportAutoDeps(selected, resolved.order);

  const navFeatures = resolved.navCapable;
  const nonNavFeatures = resolved.order.filter((id) => !navFeatures.includes(id));

  // 6. Which nav-capable features appear in the main navigation (all pre-checked).
  let tabs: string[] = navFeatures;
  if (navFeatures.length > 0) {
    const byId = new Map(catalog.map((feature) => [feature.id, feature]));
    const answer = await prompts(
      {
        type: 'multiselect',
        name: 'tabs',
        message: `Which features appear in the ${layoutLabel(layouts, layoutId)}?`,
        choices: navFeatures.map((id) => ({
          title: byId.get(id)?.nav?.label ?? id,
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

  // 7. Always-on features: enabled but not navigation tabs. Surface them so the
  // user sees where every enabled feature ends up, rather than dropping them.
  reportAlwaysOnFeatures(catalog, nonNavFeatures);

  return {
    features: selected,
    layout: layoutId,
    tabs,
    locales: locales.filter((l) => chosenLocales.includes(l.code)).map((l) => l.code),
    scheme: scheme as string,
    font,
  };
}

/**
 * The font step. The platform font comes first as the do-nothing choice, then
 * the families that cover every selected language, then the ones that do not,
 * each of those saying which languages fall back to the platform font. A
 * partially covering family is still selectable: Flutter falls back per glyph,
 * so the app works, it just renders those languages in a different face.
 */
async function promptFont(
  fonts: FontDescriptor[],
  chosen: string[],
  locales: LocaleDescriptor[],
  onCancel: () => never,
): Promise<string | undefined> {
  const covering = fonts.filter((f) => uncovered(f, chosen).length === 0);
  const partial = fonts.filter((f) => uncovered(f, chosen).length > 0);

  const { font } = await prompts(
    {
      type: 'select',
      name: 'font',
      message: 'Choose a font (optional; enter keeps the platform font)',
      choices: [
        {
          title: 'Platform default',
          description: 'The system font. Adds no package and covers every language.',
          value: '',
        },
        ...covering.map((f) => ({
          title: f.label,
          description: `${f.category === 'serif' ? 'Serif' : 'Sans-serif'} · covers every selected language`,
          value: f.id,
        })),
        ...partial.map((f) => ({
          title: f.label,
          description:
            `${f.category === 'serif' ? 'Serif' : 'Sans-serif'} · no glyphs for ` +
            `${languageList(locales, uncovered(f, chosen))}, which fall back to the platform font`,
          value: f.id,
        })),
      ],
      initial: 0,
    },
    { onCancel },
  );

  return (font as string | undefined) || undefined;
}

/** The selected languages a font has no glyphs for, in selection order. */
function uncovered(font: FontDescriptor, chosen: string[]): string[] {
  return chosen.filter((code) => !font.locales.includes(code));
}

/** Warn once about the languages a chosen font cannot draw. */
function warnCoverage(
  font: FontDescriptor,
  chosen: string[],
  locales: LocaleDescriptor[],
): void {
  const missing = uncovered(font, chosen);
  if (missing.length === 0) return;
  console.log(
    pc.yellow(
      `  ${font.label} has no glyphs for ${languageList(locales, missing)}; ` +
        'that text renders in the platform font.',
    ),
  );
}

/** English names for a set of language codes, comma-joined. */
function languageList(locales: LocaleDescriptor[], codes: string[]): string {
  return codes.map((code) => languageLabel(locales, code)).join(', ');
}

/** How the summary block names the chosen font. */
function fontLabel(fonts: FontDescriptor[], id: string | undefined): string {
  if (!id) return 'platform default';
  return fonts.find((f) => f.id === id)?.label ?? id;
}

/** The English name of a language code, for the picker's message. */
function languageLabel(locales: LocaleDescriptor[], code: string): string {
  return locales.find((l) => l.code === code)?.englishLabel ?? code;
}

/**
 * Custom `prompts` multiselect footer so the space-to-toggle interaction is
 * always visible (the default terse hint made the list feel single-select).
 */
const MULTISELECT_INSTRUCTIONS = '\n  ↑/↓ move · space toggle · a select all · enter confirm';

/** Human-readable label for a layout id, from the engine's layout descriptors. */
function layoutLabel(layouts: LayoutDescriptor[], layout: LayoutId): string {
  return layouts.find((l) => l.id === layout)?.label ?? 'main navigation';
}

/**
 * Print a distinct step listing enabled features that are not navigation tabs,
 * with a one-line note on how each integrates. These features have no tab to
 * toggle, so this is informational rather than a picker.
 */
function reportAlwaysOnFeatures(catalog: CatalogFeature[], ids: string[]): void {
  if (ids.length === 0) return;
  const byId = new Map(catalog.map((feature) => [feature.id, feature]));
  console.log(pc.bold('\nEnabled features that are not navigation tabs:'));
  for (const id of ids) {
    const feature = byId.get(id);
    // A settings-capable feature is a row inside the Settings hub; the rest
    // integrate app-wide with no screen of their own.
    const note = feature?.settingsEntry
      ? `(a row under Settings: ${feature.settingsEntry.label})`
      : '(integrates app-wide, not a tab)';
    console.log(`  - ${pc.cyan(id)} — ${feature?.summary ?? ''} ${pc.dim(note)}`);
  }
  console.log();
}

/** Print a note when selecting a feature auto-enables its dependencies. */
function reportAutoDeps(selected: string[], resolved: string[]): void {
  const chosen = new Set(selected);
  const added = resolved.filter((id) => !chosen.has(id));
  if (added.length > 0) {
    console.log(pc.dim(`  (auto-enabling required dependencies: ${added.join(', ')})`));
  }
}

/** Non-interactive resolution from flags, with sensible defaults. */
function resolveFromFlags(args: CreateArgs, layouts: LayoutDescriptor[], theme: ThemeCatalog): Setup {
  const layout = args.layout ?? 'bottom_nav';
  if (!layouts.some((l) => l.id === layout)) {
    throw new Error(
      `Unknown --layout "${layout}". Choose one of: ${layouts.map((l) => l.id).join(', ')}.`,
    );
  }
  if (args.scheme && !theme.schemes.some((s) => s.id === args.scheme)) {
    throw new Error(
      `Unknown --scheme "${args.scheme}". Choose one of: ${theme.schemes.map((s) => s.id).join(', ')}.`,
    );
  }
  const font = args.font ? theme.fonts.find((f) => f.id === args.font) : undefined;
  if (args.font && !font) {
    throw new Error(
      `Unknown --font "${args.font}". Choose one of: ${theme.fonts.map((f) => f.id).join(', ')}.`,
    );
  }
  return {
    features: args.features ?? [],
    layout: layout as LayoutId,
    tabs: args.tabs, // undefined → engine defaults to every nav-capable feature
    locales: args.locales, // undefined → engine ships every offered language
    scheme: args.scheme, // undefined → engine uses its default scheme
    font: args.font, // undefined → platform font, no font package
  };
}

async function ensureReadmeHint(dir: string): Promise<void> {
  // Non-fatal: only warn if the workspace template forgot a README.
  if (!(await fs.pathExists(path.join(dir, 'README.md')))) {
    console.log(pc.yellow('  (note: generated workspace has no README.md)'));
  }
}
