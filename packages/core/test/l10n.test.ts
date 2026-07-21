import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import os from 'node:os';
import path from 'node:path';
import fs from 'fs-extra';
import { createWorkspace } from '../src/compose.js';
import { resolveVars } from '../src/substitute.js';
import { LOCALES, resolveLocales } from '../src/l10n.js';

let tmp: string;

beforeEach(async () => {
  tmp = await fs.mkdtemp(path.join(os.tmpdir(), 'ctx0-l10n-'));
});

afterEach(async () => {
  await fs.remove(tmp);
});

async function generate(features: string[], locales?: string[]) {
  const vars = resolveVars('Acme', 'com.acme');
  const targetDir = path.join(tmp, `${vars.appSlug}-${features.join('-') || 'bare'}`);
  await createWorkspace({ targetDir, vars, features, locales });
  return { targetDir, vars };
}

describe('locale selection', () => {
  it('offers the five default languages, English first', () => {
    expect(LOCALES.map((l) => l.code)).toEqual(['en', 'el', 'de', 'fr', 'es']);
  });

  it('always includes English and answers in catalog order', () => {
    expect(resolveLocales(['fr', 'el'])).toEqual(['en', 'el', 'fr']);
    expect(resolveLocales(['en'])).toEqual(['en']);
    expect(resolveLocales()).toEqual(['en', 'el', 'de', 'fr', 'es']);
  });

  it('rejects a language it has no translations for', () => {
    expect(() => resolveLocales(['en', 'zz'])).toThrow(/Unknown language "zz"/);
  });
});

describe('composed translations', () => {
  it('emits exactly the selected languages on both sides', async () => {
    const { targetDir } = await generate(['profile'], ['en', 'el']);

    const arbDir = path.join(targetDir, 'app', 'lib', 'l10n');
    expect((await fs.readdir(arbDir)).sort()).toEqual(['app_el.arb', 'app_en.arb', 'l10n_support.dart']);

    const resources = path.join(targetDir, 'api', 'src', 'Api', 'Resources', 'Localization');
    expect((await fs.readdir(resources)).sort()).toEqual(['Messages.el.resx', 'Messages.resx']);

    const manifest = await fs.readJson(path.join(targetDir, '.ctx', 'manifest.json'));
    expect(manifest.localization).toEqual({ default: 'en', locales: ['en', 'el'] });
  });

  it('merges every enabled feature into one ARB per language', async () => {
    const { targetDir } = await generate(['profile', 'gdpr'], ['en', 'de']);

    for (const code of ['en', 'de']) {
      const arb = await fs.readJson(path.join(targetDir, 'app', 'lib', 'l10n', `app_${code}.arb`));
      expect(arb['@@locale']).toBe(code);
      // The l10n feature's shared strings, plus one key per enabled feature.
      expect(arb.commonSave).toBeTruthy();
      expect(arb.profileTitle).toBeTruthy();
      expect(arb.gdprTitle).toBeTruthy();
      expect(arb.authSignInTitle).toBeTruthy(); // pulled in as a dependency
    }

    const en = await fs.readJson(path.join(targetDir, 'app', 'lib', 'l10n', 'app_en.arb'));
    const de = await fs.readJson(path.join(targetDir, 'app', 'lib', 'l10n', 'app_de.arb'));
    expect(Object.keys(de).filter((k) => !k.startsWith('@'))).toEqual(
      Object.keys(en).filter((k) => !k.startsWith('@')),
    );
    expect(de.profileTitle).not.toBe(en.profileTitle);
    // Placeholder metadata travels with the template locale only.
    expect(en['@commonError']).toBeTruthy();
    expect(de['@commonError']).toBeUndefined();
  });

  it('generates the Dart support library from the chosen languages', async () => {
    const { targetDir } = await generate(['ping'], ['en', 'fr']);
    const support = await fs.readFile(
      path.join(targetDir, 'app', 'lib', 'l10n', 'l10n_support.dart'),
      'utf8',
    );

    expect(support).toContain("Locale('en')");
    expect(support).toContain("Locale('fr')");
    expect(support).not.toContain("Locale('de')");
    expect(support).toContain("'fr': 'Français'");
    expect(support).toContain('AppL10n.delegate');
  });

  it('generates the API culture list and message resources', async () => {
    const { targetDir } = await generate(['auth'], ['en', 'es']);

    const cultures = await fs.readFile(
      path.join(targetDir, 'api', 'src', 'Api', 'Localization', 'SupportedCultures.g.cs'),
      'utf8',
    );
    expect(cultures).toContain('"en",');
    expect(cultures).toContain('"es",');
    expect(cultures).not.toContain('"de",');

    const neutral = await fs.readFile(
      path.join(targetDir, 'api', 'src', 'Api', 'Resources', 'Localization', 'Messages.resx'),
      'utf8',
    );
    const spanish = await fs.readFile(
      path.join(targetDir, 'api', 'src', 'Api', 'Resources', 'Localization', 'Messages.es.resx'),
      'utf8',
    );
    expect(neutral).toContain('<data name="auth.invalidCredentials"');
    expect(neutral).toContain('<value>Invalid credentials.</value>');
    expect(spanish).toContain('<value>Credenciales no válidas.</value>');
  });

  it('wires the app up to the generated localizations', async () => {
    const { targetDir } = await generate(['ping']);

    const pubspec = await fs.readFile(path.join(targetDir, 'app', 'pubspec.yaml'), 'utf8');
    expect(pubspec).toContain('flutter_localizations:');
    expect(pubspec).toContain('generate: true');
    expect(await fs.pathExists(path.join(targetDir, 'app', 'l10n.yaml'))).toBe(true);

    const app = await fs.readFile(path.join(targetDir, 'app', 'lib', 'app', 'app.dart'), 'utf8');
    expect(app).toContain('supportedLocales: AppL10nSupport.supportedLocales,');
    expect(app).toContain('locale: context.watch<LocaleCubit>().state,');

    const program = await fs.readFile(
      path.join(targetDir, 'api', 'src', 'Api', 'Program.cs'),
      'utf8',
    );
    expect(program).toContain('builder.Services.AddCtxLocalization();');
    expect(program).toContain('app.UseCtxLocalization();');
  });

  it('leaves a workspace with no translatable feature alone', async () => {
    const { targetDir } = await generate([]);
    expect(await fs.pathExists(path.join(targetDir, 'app', 'lib', 'l10n'))).toBe(false);
    expect(await fs.pathExists(path.join(targetDir, 'api', 'src', 'Api', 'Resources'))).toBe(false);
  });

  it('never copies a translation fragment into the workspace', async () => {
    const { targetDir } = await generate(['profile']);
    // Fragments are named `<code>.arb` / `<code>.json`; the composed files are
    // `app_<code>.arb` and `Messages[.<code>].resx`, so a bare code is a stray.
    const strays: string[] = [];
    const walk = async (dir: string) => {
      for (const name of await fs.readdir(dir)) {
        const abs = path.join(dir, name);
        if ((await fs.stat(abs)).isDirectory()) await walk(abs);
        else if (/^(en|el|de|fr|es)\.(arb|json)$/.test(name)) strays.push(abs);
      }
    };
    await walk(targetDir);
    expect(strays).toEqual([]);
  });
});
