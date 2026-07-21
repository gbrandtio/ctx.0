import path from 'node:path';
import {
  COLOR_SCHEMES,
  DEFAULT_LOCALE,
  DEFAULT_SCHEME,
  FONTS,
  LAYOUTS,
  LOCALES,
  coreVersion,
  createWorkspace,
  generateServerSecrets,
  isLayoutId,
  isWorkspace,
  loadCatalog,
  navCapable,
  protocolVersion,
  readManifest,
  resolveFeatureOrder,
  resolveVars,
  templatesRoot,
} from '@ctx0/core';
import { CALL_SPECS, CONTRACT_VERSION, type CallName, type Calls } from './contract.js';

/**
 * The engine side of the contract: each call in `contract.ts` implemented as a
 * thin invocation of `@ctx0/core`. No composition logic lives here — arguments
 * in, engine result out.
 */

/** A call's implementation, typed against its contract entry. */
type Handler<K extends CallName> = (
  args: Calls[K]['args'],
) => Promise<Calls[K]['result']> | Calls[K]['result'];

type Handlers = { [K in CallName]: Handler<K> };

export const HANDLERS: Handlers = {
  'engine.info': (args) => ({
    engine: '@ctx0/core',
    engineVersion: coreVersion(),
    contractVersion: CONTRACT_VERSION,
    protocolVersion: protocolVersion(args.templatesRoot),
    templatesRoot: templatesRoot(args.templatesRoot),
  }),

  'catalog.list': (args) => ({
    features: [...loadCatalog(args.templatesRoot).values()].map(({ manifest }) => ({
      id: manifest.id,
      summary: manifest.summary,
      sides: manifest.sides,
      requires: manifest.requires ?? [],
      ...(manifest.nav ? { nav: manifest.nav } : {}),
    })),
  }),

  'catalog.resolve': (args) => {
    const catalog = loadCatalog(args.templatesRoot);
    const order = resolveFeatureOrder(args.features, catalog);
    return { order, navCapable: navCapable(catalog, order) };
  },

  'layouts.list': () => ({ layouts: LAYOUTS }),

  'locales.list': () => ({ locales: LOCALES, default: DEFAULT_LOCALE }),

  'theme.list': () => ({ schemes: COLOR_SCHEMES, fonts: FONTS, defaultScheme: DEFAULT_SCHEME }),

  'vars.resolve': (args) => ({ vars: resolveVars(args.name, args.org) }),

  'workspace.create': async (args) => {
    if (!path.isAbsolute(args.targetDir)) {
      throw new Error(`targetDir must be an absolute path: ${args.targetDir}`);
    }
    if (args.layout !== undefined && !isLayoutId(args.layout)) {
      throw new Error(
        `Unknown layout "${args.layout}". Known: ${LAYOUTS.map((l) => l.id).join(', ')}.`,
      );
    }

    const result = await createWorkspace({
      targetDir: args.targetDir,
      vars: resolveVars(args.name, args.org),
      features: args.features ?? [],
      layout: args.layout,
      tabs: args.tabs,
      locales: args.locales,
      scheme: args.scheme,
      font: args.font,
      scaffoldPlatforms: args.scaffoldPlatforms === true,
      toolVersion: args.toolVersion,
      templatesRoot: args.templatesRoot,
    });
    return { manifest: result.manifest, env: result.env, userSteps: result.userSteps };
  },

  'workspace.status': async (args) => {
    const catalog = loadCatalog(args.templatesRoot);
    const listed = [...catalog.values()].map(({ manifest }) => manifest);

    if (!(await isWorkspace(args.dir))) {
      return {
        isWorkspace: false,
        features: listed.map((m) => ({ id: m.id, summary: m.summary, enabled: false, tab: false })),
      };
    }

    const manifest = await readManifest(args.dir);
    // Layer ids are `<featureId>:<side>` for features; the always-on layers use
    // reserved ids that never appear in the catalog.
    const enabled = new Set(manifest.features.map((f) => f.id.split(':')[0] ?? f.id));
    const tabs = new Set(manifest.navigation?.tabs ?? []);
    return {
      isWorkspace: true,
      manifest,
      features: listed.map((m) => ({
        id: m.id,
        summary: m.summary,
        enabled: enabled.has(m.id),
        tab: tabs.has(m.id),
      })),
    };
  },

  // The contract carries the secrets as a plain name -> value map: the CLI only
  // prints them, and the engine owns which variables exist.
  'secrets.generate': () => ({ secrets: { ...generateServerSecrets() } }),
};

/**
 * Dispatch a call by name. Arguments are checked against the contract's own
 * schema — a client that speaks the contract gets a clear message about what it
 * got wrong instead of whatever the engine would have thrown downstream.
 */
export async function dispatch(name: string, args: unknown): Promise<unknown> {
  const spec = CALL_SPECS.find((c) => c.name === name);
  const handler = HANDLERS[name as CallName];
  if (!spec || !handler) {
    throw new Error(`Unknown call "${name}". Known: ${CALL_SPECS.map((c) => c.name).join(', ')}.`);
  }
  if (args !== undefined && (typeof args !== 'object' || args === null || Array.isArray(args))) {
    throw new Error(`Arguments for "${name}" must be an object.`);
  }

  const values = (args ?? {}) as Record<string, unknown>;
  const schema = spec.inputSchema as {
    required?: string[];
    properties?: Record<string, { type?: string }>;
  };
  for (const key of schema.required ?? []) {
    const expected = schema.properties?.[key]?.type;
    const value = values[key];
    if (expected === 'string' && (typeof value !== 'string' || value.length === 0)) {
      throw new Error(`"${key}" is required and must be a non-empty string.`);
    }
    if (expected === 'array' && (!Array.isArray(value) || value.some((v) => typeof v !== 'string'))) {
      throw new Error(`"${key}" is required and must be an array of strings.`);
    }
  }

  return (handler as Handler<CallName>)(values as never);
}
