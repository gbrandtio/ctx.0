import path from 'node:path';
import fs from 'fs-extra';
import { pascalCase, slugify, substitute } from './substitute.js';
import type { TemplateVars } from './types.js';

/**
 * Theming: the colour scheme and typography a workspace is generated with.
 *
 * Both choices are optional. Omitting them yields the indigo seed and the
 * platform font, which is what every workspace looked like before the choice
 * existed. Whatever is chosen ends up in one generated file:
 *
 *   app/lib/app/theme.dart    AppTheme.light() / AppTheme.dark()
 *
 * The base app imports it unconditionally, so there is no anchor to wire and no
 * template variant to keep in sync. A colour scheme is a single seed colour:
 * Material 3 derives the rest of the palette from it, which is what keeps the
 * templates' `Theme.of(context)` reads valid whichever scheme was picked.
 *
 * A font is a Google Fonts family, delivered by the `google_fonts` package. The
 * dependency is added to the mobile pubspec only when a font was chosen, so a
 * default workspace gains no package it does not use.
 */

/** A colour scheme offered by the scaffolder: one Material 3 seed colour. */
export interface ColorSchemeDescriptor {
  /** Stable id used by `--scheme` and recorded in the manifest, e.g. "teal". */
  id: string;
  /** Human-readable title shown in the picker. */
  label: string;
  /** One-line description of the resulting palette. */
  description: string;
  /** The seed as a Dart ARGB literal body, e.g. "0xFF3F51B5". */
  seed: string;
}

/** The scheme used when none is chosen: the seed generated workspaces shipped with. */
export const DEFAULT_SCHEME = 'indigo';

/** The colour schemes offered at create time, in display order. */
export const COLOR_SCHEMES: ColorSchemeDescriptor[] = [
  {
    id: 'indigo',
    label: 'Indigo',
    description: 'Deep blue-violet. The ctx.0 default.',
    seed: '0xFF3F51B5',
  },
  {
    id: 'blue',
    label: 'Blue',
    description: 'Straightforward mid blue, the most neutral of the set.',
    seed: '0xFF2196F3',
  },
  {
    id: 'teal',
    label: 'Teal',
    description: 'Blue-green, calmer than blue without going grey.',
    seed: '0xFF009688',
  },
  {
    id: 'green',
    label: 'Green',
    description: 'Natural green, readable as success rather than brand.',
    seed: '0xFF4CAF50',
  },
  {
    id: 'amber',
    label: 'Amber',
    description: 'Warm yellow-orange; the brightest surfaces of the set.',
    seed: '0xFFFFC107',
  },
  {
    id: 'deep_orange',
    label: 'Deep orange',
    description: 'Strong warm accent, close to red without colliding with errors.',
    seed: '0xFFFF5722',
  },
  {
    id: 'purple',
    label: 'Purple',
    description: 'Saturated violet for a more expressive brand.',
    seed: '0xFF9C27B0',
  },
  {
    id: 'rose',
    label: 'Rose',
    description: 'Pink-red, warm and high-contrast against neutral surfaces.',
    seed: '0xFFE91E63',
  },
  {
    id: 'slate',
    label: 'Slate',
    description: 'Desaturated blue-grey for a restrained, near-monochrome app.',
    seed: '0xFF607D8B',
  },
];

/** A font offered by the scaffolder: one Google Fonts family. */
export interface FontDescriptor {
  /** Stable id used by `--font` and recorded in the manifest, e.g. "open_sans". */
  id: string;
  /** The Google Fonts family name, e.g. "Open Sans". */
  family: string;
  /** Human-readable title shown in the picker. */
  label: string;
  /** Typeface classification, for grouping in the picker. */
  category: 'sans' | 'serif';
  /**
   * The offered languages (see `LOCALES`) this family has glyph coverage for.
   *
   * Not every family covers every language ctx.0 offers: of the five, only Greek
   * needs a subset beyond Latin, and several popular families ship no Greek
   * glyphs. A workspace generated for a language its font does not cover still
   * runs, because Flutter falls back per glyph, but that language renders in the
   * platform font, so the pickers say so before the choice is made.
   *
   * Verified against the Google Fonts metadata (`fonts.google.com/metadata/fonts`,
   * the family's `subsets`); re-check it when adding a family or a locale.
   */
  locales: string[];
}

/** Every offered language. The Latin four, i.e. everything except Greek. */
const LATIN = ['en', 'de', 'fr', 'es'];
const LATIN_AND_GREEK = ['en', 'el', 'de', 'fr', 'es'];

/** The fonts offered at create time, in display order: sans first, then serif. */
export const FONTS: FontDescriptor[] = [
  {
    id: 'inter',
    family: 'Inter',
    label: 'Inter',
    category: 'sans',
    locales: LATIN_AND_GREEK,
  },
  {
    id: 'roboto',
    family: 'Roboto',
    label: 'Roboto',
    category: 'sans',
    locales: LATIN_AND_GREEK,
  },
  {
    id: 'open_sans',
    family: 'Open Sans',
    label: 'Open Sans',
    category: 'sans',
    locales: LATIN_AND_GREEK,
  },
  {
    id: 'source_sans',
    family: 'Source Sans 3',
    label: 'Source Sans 3',
    category: 'sans',
    locales: LATIN_AND_GREEK,
  },
  {
    id: 'ibm_plex_sans',
    family: 'IBM Plex Sans',
    label: 'IBM Plex Sans',
    category: 'sans',
    locales: LATIN_AND_GREEK,
  },
  {
    id: 'noto_sans',
    family: 'Noto Sans',
    label: 'Noto Sans',
    category: 'sans',
    locales: LATIN_AND_GREEK,
  },
  {
    id: 'lato',
    family: 'Lato',
    label: 'Lato',
    category: 'sans',
    locales: LATIN,
  },
  {
    id: 'poppins',
    family: 'Poppins',
    label: 'Poppins',
    category: 'sans',
    locales: LATIN,
  },
  {
    id: 'montserrat',
    family: 'Montserrat',
    label: 'Montserrat',
    category: 'sans',
    locales: LATIN,
  },
  {
    id: 'roboto_slab',
    family: 'Roboto Slab',
    label: 'Roboto Slab',
    category: 'serif',
    locales: LATIN_AND_GREEK,
  },
  {
    id: 'merriweather',
    family: 'Merriweather',
    label: 'Merriweather',
    category: 'serif',
    locales: LATIN,
  },
];

/**
 * The `google_fonts` dependency line added to the mobile pubspec when a font is
 * chosen. Pinned at the floor that still matches the base template's Dart SDK
 * constraint rather than the newest release, which requires a newer Flutter.
 */
export const GOOGLE_FONTS_DEPENDENCY = '  google_fonts: ^6.2.1';

const SCHEME_IDS = new Set(COLOR_SCHEMES.map((s) => s.id));
const FONT_IDS = new Set(FONTS.map((f) => f.id));

/** Whether `id` is one of the offered colour schemes. */
export function isSchemeId(id: string): boolean {
  return SCHEME_IDS.has(id);
}

/** Whether `id` is one of the offered fonts. */
export function isFontId(id: string): boolean {
  return FONT_IDS.has(id);
}

/** The theme a workspace was generated with. */
export interface ThemeChoice {
  /** Colour-scheme id; the default when the user chose none. */
  scheme: string;
  /** Font id, or undefined for the platform font and no `google_fonts` dependency. */
  font?: string;
}

/**
 * Normalize a requested theme selection: unknown ids are rejected, and omitting
 * the scheme falls back to the default so two workspaces created with the same
 * answers compose identically. Omitting the font means the platform font.
 */
export function resolveTheme(scheme?: string, font?: string): ThemeChoice {
  if (scheme !== undefined && !isSchemeId(scheme)) {
    throw new Error(
      `Unknown colour scheme "${scheme}". Choose from: ${COLOR_SCHEMES.map((s) => s.id).join(', ')}.`,
    );
  }
  if (font !== undefined && !isFontId(font)) {
    throw new Error(`Unknown font "${font}". Choose from: ${FONTS.map((f) => f.id).join(', ')}.`);
  }
  return { scheme: scheme ?? DEFAULT_SCHEME, font };
}

/** Look up an offered font by id. */
export function findFont(id: string): FontDescriptor | undefined {
  return FONTS.find((f) => f.id === id);
}

/** Look up an offered colour scheme by id. */
export function findScheme(id: string): ColorSchemeDescriptor | undefined {
  return COLOR_SCHEMES.find((s) => s.id === id);
}

/**
 * The selected languages a font has no glyphs for, in the order given. Empty
 * means the font covers everything this workspace ships, which is the only case
 * that needs no warning.
 */
export function uncoveredLocales(font: FontDescriptor, locales: string[]): string[] {
  return locales.filter((code) => !font.locales.includes(code));
}

/**
 * The `GoogleFonts` method name for a family: the family lower-camel-cased with
 * its spaces removed, which is how the package names its generated accessors.
 * "Source Sans 3" -> `sourceSans3`, "IBM Plex Sans" -> `ibmPlexSans`.
 */
export function googleFontsMethod(family: string): string {
  const pascal = pascalCase(slugify(family));
  return pascal.charAt(0).toLowerCase() + pascal.slice(1);
}

/** Workspace-relative path of the generated theme. */
export const THEME_REL = path.posix.join('app', 'lib', 'app', 'theme.dart');

/**
 * Generate `app/lib/app/theme.dart` for the chosen scheme and font.
 *
 * Light and dark are both derived from the one seed, so an app that follows the
 * system brightness is themed correctly in either mode without a second choice.
 * When a font is chosen its text theme is merged *over* the Material 3 base,
 * which keeps the type scale and its brightness-correct colours.
 */
export async function composeTheme(
  workspaceRoot: string,
  choice: ThemeChoice,
  vars: TemplateVars,
): Promise<void> {
  const scheme = findScheme(choice.scheme);
  if (!scheme) {
    throw new Error(`Unknown colour scheme "${choice.scheme}".`);
  }
  const font = choice.font === undefined ? undefined : findFont(choice.font);
  if (choice.font !== undefined && !font) {
    throw new Error(`Unknown font "${choice.font}".`);
  }

  const abs = path.join(workspaceRoot, THEME_REL);
  await fs.ensureDir(path.dirname(abs));
  await fs.writeFile(abs, substitute(renderTheme(scheme, font), vars), 'utf8');
}

/** Render the theme library for one scheme and, optionally, one font. */
function renderTheme(scheme: ColorSchemeDescriptor, font: FontDescriptor | undefined): string {
  const fontImport = font ? "import 'package:google_fonts/google_fonts.dart';\n" : '';
  const typography = font
    ? `    return base.copyWith(\n` +
      `      textTheme: GoogleFonts.${googleFontsMethod(font.family)}TextTheme(base.textTheme),\n` +
      `    );\n`
    : `    return base;\n`;
  const typographyDoc = font
    ? `Typography is ${font.family}, served by the \`google_fonts\` package.`
    : 'Typography is the platform font: no font package is involved.';

  return `import 'package:flutter/material.dart';
${fontImport}
/// The colour scheme and typography CtxApp was generated with. Generated by
/// ctx.0 from the theme selection made at create time. Regenerate it by
/// re-running the scaffolder; do not edit by hand.
///
/// Every colour is derived from [seed], so rebranding the app is a change to
/// that one value.
///
/// ${typographyDoc}
class AppTheme {
  const AppTheme._();

  /// The ${scheme.label.toLowerCase()} seed every colour in the app is derived from.
  static const Color seed = Color(${scheme.seed});

  /// The light theme, for [MaterialApp.theme].
  static ThemeData light() => _theme(Brightness.light);

  /// The dark theme, for [MaterialApp.darkTheme].
  static ThemeData dark() => _theme(Brightness.dark);

  static ThemeData _theme(Brightness brightness) {
    final ThemeData base = ThemeData(
      colorScheme: ColorScheme.fromSeed(
        seedColor: seed,
        brightness: brightness,
      ),
      useMaterial3: true,
    );
${typography}  }
}
`;
}
