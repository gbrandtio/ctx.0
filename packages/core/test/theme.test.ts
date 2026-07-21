import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import os from 'node:os';
import path from 'node:path';
import fs from 'fs-extra';
import { resolveVars } from '../src/substitute.js';
import {
  COLOR_SCHEMES,
  DEFAULT_SCHEME,
  FONTS,
  composeTheme,
  findFont,
  googleFontsMethod,
  resolveTheme,
  uncoveredLocales,
  THEME_REL,
} from '../src/theme.js';
import { LOCALES } from '../src/l10n.js';

let tmp: string;

beforeEach(async () => {
  tmp = await fs.mkdtemp(path.join(os.tmpdir(), 'ctx0-theme-'));
});

afterEach(async () => {
  await fs.remove(tmp);
});

/** Generate the theme library alone and read it back. */
async function generateTheme(scheme?: string, font?: string): Promise<string> {
  const vars = resolveVars('Acme', 'com.acme');
  await composeTheme(tmp, resolveTheme(scheme, font), vars);
  return fs.readFile(path.join(tmp, THEME_REL), 'utf8');
}

describe('theme selection', () => {
  it('defaults to the indigo seed and the platform font', () => {
    expect(resolveTheme()).toEqual({ scheme: DEFAULT_SCHEME, font: undefined });
    expect(COLOR_SCHEMES.find((s) => s.id === DEFAULT_SCHEME)?.seed).toBe('0xFF3F51B5');
  });

  it('keeps both choices independent', () => {
    expect(resolveTheme('teal')).toEqual({ scheme: 'teal', font: undefined });
    expect(resolveTheme(undefined, 'inter')).toEqual({ scheme: DEFAULT_SCHEME, font: 'inter' });
  });

  it('rejects a scheme or font it does not offer', () => {
    expect(() => resolveTheme('chartreuse')).toThrow(/Unknown colour scheme "chartreuse"/);
    expect(() => resolveTheme('teal', 'comic_sans')).toThrow(/Unknown font "comic_sans"/);
  });
});

describe('font coverage', () => {
  it('reports the selected languages a family cannot draw', () => {
    const lato = findFont('lato')!;
    expect(uncoveredLocales(lato, ['en', 'el', 'de'])).toEqual(['el']);
    expect(uncoveredLocales(lato, ['en', 'de', 'fr'])).toEqual([]);
  });

  it('reports nothing for a family that covers every offered language', () => {
    const inter = findFont('inter')!;
    expect(uncoveredLocales(inter, LOCALES.map((l) => l.code))).toEqual([]);
  });

  it('only ever claims coverage of languages the scaffolder offers', () => {
    const offered = new Set(LOCALES.map((l) => l.code));
    for (const font of FONTS) {
      for (const code of font.locales) {
        expect(offered.has(code), `${font.id} claims unknown locale ${code}`).toBe(true);
      }
    }
  });

  it('names a google_fonts accessor that is a valid Dart identifier', () => {
    for (const font of FONTS) {
      expect(googleFontsMethod(font.family)).toMatch(/^[a-z][A-Za-z0-9]*$/);
    }
    expect(googleFontsMethod('Source Sans 3')).toBe('sourceSans3');
    expect(googleFontsMethod('IBM Plex Sans')).toBe('ibmPlexSans');
  });
});

describe('generated theme', () => {
  it('derives light and dark from the chosen seed', async () => {
    const dart = await generateTheme('teal');

    expect(dart).toContain('static const Color seed = Color(0xFF009688);');
    expect(dart).toContain('static ThemeData light() => _theme(Brightness.light);');
    expect(dart).toContain('static ThemeData dark() => _theme(Brightness.dark);');
    expect(dart).toContain('ColorScheme.fromSeed(');
    expect(dart).toContain('seedColor: seed,');
    expect(dart).toContain('brightness: brightness,');
  });

  it('leaves out google_fonts entirely when no font was chosen', async () => {
    const dart = await generateTheme();

    expect(dart).not.toContain('google_fonts');
    expect(dart).toContain('return base;');
    expect(dart).toContain('the platform font');
  });

  it('merges the chosen font over the Material text theme', async () => {
    const dart = await generateTheme('purple', 'source_sans');

    expect(dart).toContain("import 'package:google_fonts/google_fonts.dart';");
    expect(dart).toContain('textTheme: GoogleFonts.sourceSans3TextTheme(base.textTheme)');
  });
});
