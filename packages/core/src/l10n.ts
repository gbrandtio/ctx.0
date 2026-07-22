import path from 'node:path';
import fs from 'fs-extra';
import { substitute } from './substitute.js';
import { sortUtf8 } from './order.js';
import type { Side, TemplateVars } from './types.js';

/**
 * Localization: the languages a workspace is generated with.
 *
 * Translations are *not* copied from overlays like ordinary files. Each overlay
 * may ship a root-level `l10n/` directory of per-locale fragments — `<code>.arb`
 * on the mobile side, `<code>.json` on the api side — which `copyTree` skips as
 * engine metadata. This module merges the fragments of every enabled layer, in
 * application order, into the two artifacts each ecosystem expects:
 *
 *   app/lib/l10n/app_<code>.arb        Flutter `gen-l10n` sources
 *   app/lib/l10n/l10n_support.dart     supported locales + delegates + names
 *   api/src/Api/Resources/Messages[.<code>].resx   .NET `IStringLocalizer` sources
 *
 * Only the selected locales are emitted, so a workspace created for English and
 * Greek carries exactly two translations rather than five filtered at runtime.
 */

/** A language offered by the scaffolder. */
export interface LocaleDescriptor {
  /** BCP-47 language code, e.g. "el". */
  code: string;
  /** The language's name in the language itself, e.g. "Ελληνικά". */
  label: string;
  /** The language's name in English, e.g. "Greek". */
  englishLabel: string;
}

/**
 * The fallback locale. It is the `gen-l10n` template ARB and the .NET *neutral*
 * resource file, so it is always generated regardless of what the user selects.
 */
export const DEFAULT_LOCALE = 'en';

/** The languages offered at create time, in display (and emission) order. */
export const LOCALES: LocaleDescriptor[] = [
  { code: 'en', label: 'English', englishLabel: 'English' },
  { code: 'el', label: 'Ελληνικά', englishLabel: 'Greek' },
  { code: 'de', label: 'Deutsch', englishLabel: 'German' },
  { code: 'fr', label: 'Français', englishLabel: 'French' },
  { code: 'es', label: 'Español', englishLabel: 'Spanish' },
];

const LOCALE_CODES = new Set(LOCALES.map((l) => l.code));

/** Whether `code` is one of the offered languages. */
export function isLocaleCode(code: string): boolean {
  return LOCALE_CODES.has(code);
}

/**
 * Normalize a requested language selection: unknown codes are rejected, the
 * default locale is always included, and the result is returned in catalog
 * order so two workspaces with the same languages compose identically.
 * Omitting the selection enables every offered language.
 */
export function resolveLocales(requested?: string[]): string[] {
  if (!requested) return LOCALES.map((l) => l.code);
  for (const code of requested) {
    if (!isLocaleCode(code)) {
      throw new Error(
        `Unknown language "${code}". Choose from: ${LOCALES.map((l) => l.code).join(', ')}.`,
      );
    }
  }
  const wanted = new Set([DEFAULT_LOCALE, ...requested]);
  return LOCALES.filter((l) => wanted.has(l.code)).map((l) => l.code);
}

/** Overlay root that may carry an `l10n/` fragment directory, with its side. */
export interface LocaleSource {
  /** Absolute path of the overlay/base source dir. */
  dir: string;
  side: Side;
}

/** Workspace-relative locations of the generated localization artifacts. */
export const ARB_DIR_REL = path.posix.join('app', 'lib', 'l10n');
export const L10N_SUPPORT_REL = path.posix.join(ARB_DIR_REL, 'l10n_support.dart');
export const RESOURCES_DIR_REL = path.posix.join('api', 'src', 'Api', 'Resources', 'Localization');
export const LOCALIZATION_DIR_REL = path.posix.join('api', 'src', 'Api', 'Localization');
export const SUPPORTED_CULTURES_REL = path.posix.join(LOCALIZATION_DIR_REL, 'SupportedCultures.g.cs');

/** The directory name an overlay ships its translation fragments in. */
export const L10N_FRAGMENT_DIR = 'l10n';

/**
 * Merge every source's translation fragments into the workspace's ARB files,
 * resource files and the Dart support library.
 *
 * A fragment missing for a selected locale is not an error: the key simply does
 * not appear in that locale, and both Flutter and .NET fall back to the default
 * locale at runtime. A key defined by two different sources *is* an error — the
 * winner would depend on application order.
 */
export async function composeLocales(
  workspaceRoot: string,
  locales: string[],
  sources: LocaleSource[],
  vars: TemplateVars,
): Promise<void> {
  const mobile = sources.filter((s) => s.side === 'mobile');
  const api = sources.filter((s) => s.side === 'api');

  let anyMessages = false;
  for (const code of locales) {
    const arb = mergeFragments(mobile, code, '.arb', vars);
    if (Object.keys(arb).length > 0) {
      await writeArb(workspaceRoot, code, arb);
    }
    const messages = mergeFragments(api, code, '.json', vars);
    if (Object.keys(messages).length > 0) {
      // The default locale is the *neutral* resource set — the one every other
      // culture falls back to — rather than a culture of its own.
      await writeResx(workspaceRoot, code === DEFAULT_LOCALE ? undefined : code, messages);
      anyMessages = true;
    }
  }

  // The mobile support library belongs to the mandatory session layer, which
  // always wires the MaterialApp delegates and `supportedLocales`, so it is
  // generated for every workspace. The session overlay always contributes the
  // app title and common strings, so there is always an ARB to back it.
  if (await fs.pathExists(path.join(workspaceRoot, 'app', 'lib', 'session'))) {
    await writeSupportLibrary(workspaceRoot, locales, vars);
  }
  // The API localization bootstrap ships in the always-on base, so its directory
  // is present in every workspace and the culture list + neutral resource set are
  // generated unconditionally — the API answers in the caller's language even
  // with no features enabled.
  if (await fs.pathExists(path.join(workspaceRoot, LOCALIZATION_DIR_REL))) {
    await writeSupportedCultures(workspaceRoot, locales, vars);
    // The resource set must exist even when no enabled feature has a message
    // yet, so `IStringLocalizer<Messages>` always has something to bind to.
    if (!anyMessages) await writeResx(workspaceRoot, undefined, {});
  }
}

/**
 * Read and merge one locale's fragments across sources, in application order.
 * ARB metadata keys (`@key`, `@@locale`) travel with their message but do not
 * participate in duplicate detection.
 */
function mergeFragments(
  sources: LocaleSource[],
  code: string,
  ext: string,
  vars: TemplateVars,
): Record<string, unknown> {
  const merged: Record<string, unknown> = {};
  const owner = new Map<string, string>();

  for (const source of sources) {
    const file = path.join(source.dir, L10N_FRAGMENT_DIR, `${code}${ext}`);
    if (!fs.existsSync(file)) continue;
    const raw = substitute(fs.readFileSync(file, 'utf8'), vars);
    let fragment: Record<string, unknown>;
    try {
      fragment = JSON.parse(raw) as Record<string, unknown>;
    } catch (err) {
      throw new Error(`Invalid translation fragment ${file}: ${(err as Error).message}`);
    }
    for (const [key, value] of Object.entries(fragment)) {
      if (key.startsWith('@@')) continue; // header, re-emitted per file
      if (!key.startsWith('@')) {
        const previous = owner.get(key);
        if (previous) {
          throw new Error(
            `Duplicate translation key "${key}" for locale "${code}": defined by ${previous} and ${file}.`,
          );
        }
        owner.set(key, file);
      }
      merged[key] = value;
    }
  }
  return merged;
}

/** Write `app/lib/l10n/app_<code>.arb`. */
async function writeArb(
  workspaceRoot: string,
  code: string,
  messages: Record<string, unknown>,
): Promise<void> {
  const abs = path.join(workspaceRoot, ARB_DIR_REL, `app_${code}.arb`);
  await fs.ensureDir(path.dirname(abs));
  const body = { '@@locale': code, ...messages };
  await fs.writeFile(abs, `${JSON.stringify(body, null, 2)}\n`, 'utf8');
}

/**
 * Write a .NET resource file — `<code>` for one culture, or the culture-neutral
 * fallback set when it is omitted.
 *
 * The path is not free: `IStringLocalizer<Messages>` resolves its resources as
 * `<root namespace>.<ResourcesPath>.<type minus root namespace>`, which is why
 * these land in `Resources/Localization/` next to the `Messages` marker type's
 * namespace. The API declares its root namespace to the localizer in
 * `Api/Localization/RootNamespace.cs`.
 */
async function writeResx(
  workspaceRoot: string,
  code: string | undefined,
  messages: Record<string, unknown>,
): Promise<void> {
  const name = code === undefined ? 'Messages.resx' : `Messages.${code}.resx`;
  const abs = path.join(workspaceRoot, RESOURCES_DIR_REL, name);
  await fs.ensureDir(path.dirname(abs));
  await fs.writeFile(abs, renderResx(messages), 'utf8');
}

/** Render the canonical resx document for a flat key -> string map. */
function renderResx(messages: Record<string, unknown>): string {
  const entries = sortUtf8(Object.keys(messages))
    .map(
      (key) =>
        `  <data name="${xml(key)}" xml:space="preserve">\n` +
        `    <value>${xml(String(messages[key]))}</value>\n` +
        `  </data>`,
    )
    .join('\n');
  return `<?xml version="1.0" encoding="utf-8"?>
<root>
  <resheader name="resmimetype">
    <value>text/microsoft-resx</value>
  </resheader>
  <resheader name="version">
    <value>2.0</value>
  </resheader>
  <resheader name="reader">
    <value>System.Resources.ResXResourceReader, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a2c561934e089</value>
  </resheader>
  <resheader name="writer">
    <value>System.Resources.ResXResourceWriter, System.Windows.Forms, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a2c561934e089</value>
  </resheader>
${entries}
</root>
`;
}

function xml(value: string): string {
  return value
    .split('&')
    .join('&amp;')
    .split('<')
    .join('&lt;')
    .split('>')
    .join('&gt;')
    .split('"')
    .join('&quot;');
}

/**
 * Generate the Dart support library: the delegates and supported locales the
 * root `MaterialApp` needs, plus the display name of each language for the
 * in-app picker. Generated (rather than shipped) so it names exactly the
 * locales this workspace was created with.
 */
async function writeSupportLibrary(
  workspaceRoot: string,
  locales: string[],
  vars: TemplateVars,
): Promise<void> {
  const byCode = new Map(LOCALES.map((l) => [l.code, l]));
  const supported = locales.map((code) => `    Locale('${code}'),`).join('\n');
  const names = locales
    .map((code) => {
      const descriptor = byCode.get(code)!;
      return `    '${code}': '${dartEscape(descriptor.label)}',`;
    })
    .join('\n');

  const content = `import 'package:flutter/widgets.dart';
import 'package:flutter_localizations/flutter_localizations.dart';

import 'gen/app_l10n.dart';

/// The languages CtxApp was generated with, and the wiring [MaterialApp] needs
/// to use them. Generated by ctx.0 from the locale selection made at create
/// time — regenerate it by re-running the scaffolder, do not edit by hand.
class AppL10nSupport {
  const AppL10nSupport._();

  /// The fallback language: the ARB template every other locale falls back to.
  static const Locale defaultLocale = Locale('${DEFAULT_LOCALE}');

  /// Every language this app ships translations for, in catalog order.
  static const List<Locale> supportedLocales = <Locale>[
${supported}
  ];

  /// The delegates that resolve [AppL10n] and the Material/Cupertino/Widgets
  /// strings for the supported locales.
  static const List<LocalizationsDelegate<Object?>> delegates =
      <LocalizationsDelegate<Object?>>[
    AppL10n.delegate,
    GlobalMaterialLocalizations.delegate,
    GlobalWidgetsLocalizations.delegate,
    GlobalCupertinoLocalizations.delegate,
  ];

  static const Map<String, String> _names = <String, String>{
${names}
  };

  /// The language's own name, for the language picker.
  static String languageName(Locale locale) => _names[locale.languageCode] ?? locale.languageCode;
}
`;
  const abs = path.join(workspaceRoot, L10N_SUPPORT_REL);
  await fs.ensureDir(path.dirname(abs));
  await fs.writeFile(abs, substitute(content, vars), 'utf8');
}

/**
 * Generate the API's culture list. Satellite assemblies cannot be enumerated
 * reliably at startup, so the languages the workspace was created with are
 * written out as code and read by `AddCtxLocalization`.
 */
async function writeSupportedCultures(
  workspaceRoot: string,
  locales: string[],
  vars: TemplateVars,
): Promise<void> {
  const codes = locales.map((code) => `        "${code}",`).join('\n');
  const content = `namespace CtxApp.Api.Localization;

/// <summary>
/// The languages this workspace was generated with, in catalog order. Generated
/// by ctx.0 from the locale selection made at create time — regenerate it by
/// re-running the scaffolder, do not edit by hand.
/// </summary>
public static class SupportedCultures
{
    /// <summary>Culture codes, the first of which is the fallback culture.</summary>
    public static readonly string[] Codes =
    [
${codes}
    ];
}
`;
  const abs = path.join(workspaceRoot, SUPPORTED_CULTURES_REL);
  await fs.ensureDir(path.dirname(abs));
  await fs.writeFile(abs, substitute(content, vars), 'utf8');
}

function dartEscape(value: string): string {
  return value.replace(/\\/g, '\\\\').replace(/'/g, "\\'");
}
