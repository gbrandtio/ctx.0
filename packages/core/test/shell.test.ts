import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import os from 'node:os';
import path from 'node:path';
import fs from 'fs-extra';
import { composeShell, navCapable, isLayoutId, SHELL_REL, LAYOUTS } from '../src/shell.js';
import { resolveVars } from '../src/substitute.js';
import type { CatalogEntry } from '../src/catalog.js';
import type { FeatureManifest } from '../src/types.js';

let tmp: string;
const vars = resolveVars('Acme', 'com.acme');

beforeEach(async () => {
  tmp = await fs.mkdtemp(path.join(os.tmpdir(), 'ctx0-shell-'));
});
afterEach(async () => {
  await fs.remove(tmp);
});

/** Minimal catalog with one nav-capable feature (ping) and one without (auth). */
function fakeCatalog(): Map<string, CatalogEntry> {
  const ping: FeatureManifest = {
    id: 'ping',
    summary: 'ping',
    sides: ['mobile'],
    nav: {
      label: "It's ping",
      icon: 'lock',
      page: 'PingPage',
      import: '../features/ping/views/ping_page.dart',
    },
  };
  const auth: FeatureManifest = { id: 'auth', summary: 'auth', sides: ['mobile'] };
  return new Map<string, CatalogEntry>([
    ['ping', { manifest: ping, dirs: {} }],
    ['auth', { manifest: auth, dirs: {} }],
  ]);
}

async function read(root: string): Promise<string> {
  return fs.readFile(path.join(root, SHELL_REL), 'utf8');
}

describe('shell helpers', () => {
  it('recognises every advertised layout id', () => {
    for (const l of LAYOUTS) expect(isLayoutId(l.id)).toBe(true);
    expect(isLayoutId('nope')).toBe(false);
  });

  it('navCapable keeps only features that declare nav, in order', () => {
    const catalog = fakeCatalog();
    expect(navCapable(catalog, ['auth', 'ping'])).toEqual(['ping']);
    expect(navCapable(catalog, ['auth'])).toEqual([]);
  });
});

describe('composeShell', () => {
  it('renders each layout with the tab wired in and the label escaped', async () => {
    const catalog = fakeCatalog();
    for (const { id } of LAYOUTS) {
      const root = path.join(tmp, id);
      await composeShell(root, id, ['ping'], catalog, vars);
      const shell = await read(root);
      expect(shell).toContain('class CtxShell');
      expect(shell).toContain("import '../features/ping/views/ping_page.dart';");
      // Single-quote in the label is escaped for the Dart literal.
      expect(shell).toContain("It\\'s ping");
      expect(shell).not.toContain('ctx:gen:');
    }
  });

  it('emits a placeholder (no destinations) when there are no tabs', async () => {
    const root = path.join(tmp, 'empty');
    await composeShell(root, 'bottom_nav', [], fakeCatalog(), vars);
    const shell = await read(root);
    expect(shell).toContain('placeholder');
    expect(shell).not.toContain('NavigationBar');
    expect(shell).not.toContain('../features/');
  });

  it('substitutes the app token in generated shells', async () => {
    const root = path.join(tmp, 'subst');
    await composeShell(root, 'drawer', ['ping'], fakeCatalog(), vars);
    const shell = await read(root);
    expect(shell).toContain("const Text('Acme')");
    expect(shell).not.toContain('CtxApp');
  });

  it('rejects a tab that is not nav-capable', async () => {
    const root = path.join(tmp, 'bad');
    await expect(composeShell(root, 'bottom_nav', ['auth'], fakeCatalog(), vars)).rejects.toThrow(
      /not nav-capable/,
    );
  });

  it('rejects an unknown layout', async () => {
    const root = path.join(tmp, 'badlayout');
    await expect(
      // @ts-expect-error deliberately passing an invalid layout id
      composeShell(root, 'spaceship', ['ping'], fakeCatalog(), vars),
    ).rejects.toThrow(/Unknown layout/);
  });
});
