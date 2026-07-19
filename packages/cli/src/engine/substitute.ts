import type { TemplateVars } from './types.js';

/**
 * Substitution tokens used throughout the template trees. They are chosen to be
 * *real, valid identifiers* so that every `base`/overlay tree compiles and runs
 * on its own (that is the whole point of the overlay approach) while remaining
 * distinctive enough to rewrite without collisions.
 *
 *   CtxApp        -> PascalCase app/type name   (namespaces, class prefixes)
 *   ctxapp        -> lower/snake app slug        (dart package name, folders)
 *   com.ctx.app   -> reverse-DNS bundle id       (android/ios/csproj ids)
 *
 * Replacement is ordered most-specific-first so the dotted bundle id is handled
 * before the bare tokens. Matching is case-sensitive; `ctxapp` never matches
 * `CtxApp`.
 */
export const TOKENS = {
  bundle: 'com.ctx.app',
  pascal: 'CtxApp',
  slug: 'ctxapp',
} as const;

/** Derive the full set of substitution variables from user input. */
export function resolveVars(appNameInput: string, orgInput?: string): TemplateVars {
  const appSlug = slugify(appNameInput);
  if (!appSlug) {
    throw new Error(`Invalid application name: "${appNameInput}"`);
  }
  const appName = pascalCase(appSlug);
  const org = normalizeOrg(orgInput) ?? `com.${appSlug}`;
  const bundleId = `${org}.app`;
  return { appName, appSlug, org, bundleId };
}

/** Apply token substitution to a string (file content or a path segment). */
export function substitute(input: string, vars: TemplateVars): string {
  return input
    .split(TOKENS.bundle)
    .join(vars.bundleId)
    .split(TOKENS.pascal)
    .join(vars.appName)
    .split(TOKENS.slug)
    .join(vars.appSlug);
}

/** Whether a file appears to be binary (skip substitution to avoid corruption). */
export function isProbablyBinary(buf: Buffer): boolean {
  const len = Math.min(buf.length, 8000);
  for (let i = 0; i < len; i++) {
    if (buf[i] === 0) return true;
  }
  return false;
}

export function slugify(input: string): string {
  return input
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '_')
    .replace(/^_+|_+$/g, '')
    .replace(/_{2,}/g, '_');
}

export function pascalCase(slug: string): string {
  return slug
    .split('_')
    .filter(Boolean)
    .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
    .join('');
}

function normalizeOrg(org?: string): string | undefined {
  if (!org) return undefined;
  const cleaned = org
    .trim()
    .toLowerCase()
    .replace(/[^a-z0-9.]+/g, '.')
    .replace(/^\.+|\.+$/g, '')
    .replace(/\.{2,}/g, '.');
  return cleaned || undefined;
}
