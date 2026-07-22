import path from 'node:path';
import fs from 'fs-extra';
import { substitute } from './substitute.js';
import type { CatalogEntry } from './catalog.js';
import type { SettingsEntry, TemplateVars } from './types.js';

/** Workspace-relative path of the `settings` feature's hub page. */
export const SETTINGS_PAGE_REL = path.posix.join(
  'app',
  'lib',
  'features',
  'settings',
  'views',
  'settings_page.dart',
);

/**
 * From an ordered list of enabled feature ids, return those that are
 * settings-capable (declare a `settingsEntry`), preserving order. This is the
 * set of rows the Settings hub is generated from — the settings-hub analogue of
 * {@link import('./shell.js').navCapable}.
 */
export function settingsCapable(catalog: Map<string, CatalogEntry>, enabled: string[]): string[] {
  return enabled.filter((id) => catalog.get(id)?.manifest.settingsEntry);
}

/**
 * Fill the `settings` feature's `SettingsPage` with a row per settings-capable
 * feature. The page ships from the overlay with `ctx:gen:settings-imports` and
 * `ctx:gen:settings-entries` markers, already copied into the workspace; this
 * replaces those markers with Dart derived from each entry's `settingsEntry`
 * metadata, in `entries` order. Every id must be present in the catalog and
 * declare a `settingsEntry`.
 *
 * A no-op when the `settings` feature is not enabled (its page is absent). With
 * the page present but no entries, the markers are dropped and the page renders
 * its built-in empty state.
 */
export async function composeSettings(
  workspaceRoot: string,
  entries: string[],
  catalog: Map<string, CatalogEntry>,
  vars: TemplateVars,
): Promise<void> {
  const abs = path.join(workspaceRoot, SETTINGS_PAGE_REL);
  if (!(await fs.pathExists(abs))) return; // settings feature not enabled

  const metas = entries.map((id) => {
    const entry = catalog.get(id);
    if (!entry) {
      throw new Error(`Settings entry "${id}" is not a known feature.`);
    }
    if (!entry.manifest.settingsEntry) {
      throw new Error(
        `Settings entry "${id}" is not settings-capable (its feature.json declares no "settingsEntry").`,
      );
    }
    return entry.manifest.settingsEntry;
  });

  let src = await fs.readFile(abs, 'utf8');
  src = replaceMarker(src, 'ctx:gen:settings-imports', imports(metas));
  src = replaceMarker(src, 'ctx:gen:settings-entries', metas.length === 0 ? emptyState() : tiles(metas));
  await fs.writeFile(abs, substitute(src, vars), 'utf8');
}

function imports(metas: SettingsEntry[]): string {
  return metas.map((m) => `import '${packageImport(m.import)}';`).join('\n');
}

/**
 * A `settingsEntry.import` is app-relative to the shell's location
 * (`app/lib/app/`, e.g. `../features/l10n/views/language_page.dart`), the same
 * form the nav shell uses. The Settings page sits deeper
 * (`app/lib/features/settings/views/`), so a relative import would not resolve
 * from there. Rewrite the leading `../` to a package import, which is
 * location-independent; the `ctxapp` token is substituted to the app slug.
 */
function packageImport(appRelative: string): string {
  return appRelative.replace(/^(\.\.\/)+/, 'package:ctxapp/');
}

function tiles(metas: SettingsEntry[]): string {
  return metas
    .map(
      (m) =>
        `          ListTile(leading: const Icon(Icons.${m.icon}), title: const Text(${dartString(m.label)}), ` +
        `trailing: const Icon(Icons.chevron_right), ` +
        `onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => const ${m.page}()))),`,
    )
    .join('\n');
}

/**
 * The row list when no enabled feature is settings-capable: a single centred,
 * localised sentence, per the UI/UX guidelines' empty-state rule.
 */
function emptyState(): string {
  return (
    "          Padding(padding: const EdgeInsets.all(16), " +
    "child: Center(child: Text(AppL10n.of(context).settingsEmpty))),"
  );
}

/** Render a Dart single-quoted string literal, escaping backslashes and quotes. */
function dartString(value: string): string {
  return `'${value.replace(/\\/g, '\\\\').replace(/'/g, "\\'")}'`;
}

/** Replace the single line carrying `marker` with `block` (dropped if empty). */
function replaceMarker(src: string, marker: string, block: string): string {
  const lines = src.split('\n');
  const idx = lines.findIndex((l) => l.includes(marker));
  if (idx === -1) {
    throw new Error(`SettingsPage template is missing the "${marker}" marker.`);
  }
  if (block === '') {
    lines.splice(idx, 1);
  } else {
    lines.splice(idx, 1, block);
  }
  return lines.join('\n');
}
