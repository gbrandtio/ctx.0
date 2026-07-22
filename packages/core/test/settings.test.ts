import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import os from 'node:os';
import path from 'node:path';
import fs from 'fs-extra';
import { composeSettings, settingsCapable, SETTINGS_PAGE_REL } from '../src/settings.js';
import { resolveVars } from '../src/substitute.js';
import type { CatalogEntry } from '../src/catalog.js';
import type { FeatureManifest } from '../src/types.js';

let tmp: string;
const vars = resolveVars('Acme', 'com.acme');

beforeEach(async () => {
  tmp = await fs.mkdtemp(path.join(os.tmpdir(), 'ctx0-settings-'));
});
afterEach(async () => {
  await fs.remove(tmp);
});

/** Catalog with one settings-capable feature (l10n), the hub (settings), and one plain (auth). */
function fakeCatalog(): Map<string, CatalogEntry> {
  const l10n: FeatureManifest = {
    id: 'l10n',
    summary: 'l10n',
    sides: ['mobile'],
    settingsEntry: {
      label: "It's language",
      icon: 'translate',
      page: 'LanguagePage',
      import: '../features/l10n/views/language_page.dart',
    },
  };
  const settings: FeatureManifest = { id: 'settings', summary: 'settings', sides: ['mobile'] };
  const auth: FeatureManifest = { id: 'auth', summary: 'auth', sides: ['mobile'] };
  return new Map<string, CatalogEntry>([
    ['l10n', { manifest: l10n, dirs: {} }],
    ['settings', { manifest: settings, dirs: {} }],
    ['auth', { manifest: auth, dirs: {} }],
  ]);
}

/** Write a SettingsPage template carrying the two generation markers. */
async function seedPage(root: string): Promise<void> {
  const abs = path.join(root, SETTINGS_PAGE_REL);
  await fs.ensureDir(path.dirname(abs));
  await fs.writeFile(
    abs,
    [
      "import 'package:flutter/material.dart';",
      "import 'package:ctxapp/l10n/gen/app_l10n.dart';",
      '// ctx:gen:settings-imports',
      'class SettingsPage extends StatelessWidget {',
      "  const Text('CtxApp');",
      '  body: ListView(children: <Widget>[',
      '          // ctx:gen:settings-entries',
      '  ]);',
      '}',
    ].join('\n'),
    'utf8',
  );
}

async function read(root: string): Promise<string> {
  return fs.readFile(path.join(root, SETTINGS_PAGE_REL), 'utf8');
}

describe('settingsCapable', () => {
  it('keeps only features that declare settingsEntry, in order', () => {
    const catalog = fakeCatalog();
    expect(settingsCapable(catalog, ['auth', 'settings', 'l10n'])).toEqual(['l10n']);
    expect(settingsCapable(catalog, ['auth', 'settings'])).toEqual([]);
  });
});

describe('composeSettings', () => {
  it('is a no-op when the settings page is absent (feature not enabled)', async () => {
    // No page seeded; must not throw and must not create one.
    await composeSettings(tmp, ['l10n'], fakeCatalog(), vars);
    expect(await fs.pathExists(path.join(tmp, SETTINGS_PAGE_REL))).toBe(false);
  });

  it('renders a row per settings-capable feature, escaped and substituted', async () => {
    await seedPage(tmp);
    await composeSettings(tmp, ['l10n'], fakeCatalog(), vars);
    const page = await read(tmp);
    // The app-relative manifest import is rewritten to a location-independent
    // package import, with the app slug substituted for the ctxapp token.
    expect(page).toContain("import 'package:acme/features/l10n/views/language_page.dart';");
    expect(page).toContain('Icons.translate');
    expect(page).toContain('LanguagePage()');
    // Single-quote in the label is escaped for the Dart literal.
    expect(page).toContain("It\\'s language");
    // Token substitution still runs over the page.
    expect(page).toContain("const Text('Acme')");
    expect(page).not.toContain('ctx:gen:');
  });

  it('emits the localised empty state when no feature is settings-capable', async () => {
    await seedPage(tmp);
    await composeSettings(tmp, [], fakeCatalog(), vars);
    const page = await read(tmp);
    expect(page).toContain('settingsEmpty');
    expect(page).not.toContain('LanguagePage');
    expect(page).not.toContain('ctx:gen:');
  });

  it('rejects an entry that is not settings-capable', async () => {
    await seedPage(tmp);
    await expect(composeSettings(tmp, ['auth'], fakeCatalog(), vars)).rejects.toThrow(
      /not settings-capable/,
    );
  });
});
