/**
 * The ctx.0 engine contract.
 *
 * This module is the *only* thing the CLI and the engine share: the set of calls
 * the engine answers, their arguments, and their results. The CLI does not
 * import `@ctx0/core` — it spawns the engine and talks to it over JSON-RPC 2.0
 * on stdio (MCP), so either side can be replaced by an implementation in another
 * language as long as it honours what is declared here.
 *
 * It deliberately holds no logic and no dependency on the engine: types and JSON
 * Schemas only. `tools.ts` implements the contract over `@ctx0/core`;
 * `../../cli` consumes it through `Engine` in the CLI's `engine.ts`.
 */

/** Bumped when a call's arguments or result change shape. Reported by `engine.info`. */
export const CONTRACT_VERSION = '2';

export type Side = 'mobile' | 'api';
export type LayoutId = 'bottom_nav' | 'nav_rail' | 'drawer' | 'home_list';

/** The substitution variables derived from an application name. */
export interface Vars {
  appName: string;
  appSlug: string;
  org: string;
  bundleId: string;
}

/** A feature as the catalog reports it. `nav` is present iff it can be a tab. */
export interface CatalogFeature {
  id: string;
  summary: string;
  sides: Side[];
  requires: string[];
  nav?: { label: string; icon: string; page: string; import: string };
}

/** A language the workspace can be generated with. */
export interface LocaleDescriptor {
  /** BCP-47 language code, e.g. "el". */
  code: string;
  /** The language's name in the language itself, e.g. "Ελληνικά". */
  label: string;
  /** The language's name in English, e.g. "Greek". */
  englishLabel: string;
}

/** A selectable main-navigation layout. */
export interface LayoutDescriptor {
  id: LayoutId;
  label: string;
  description: string;
}

/** The workspace manifest, as far as a frontend needs to read it. */
export interface WorkspaceManifest {
  schema: number;
  ctx0Version: string;
  protocolVersion: string;
  vars: Vars;
  features: { id: string; files: string[]; hash: string }[];
  navigation: { layout: LayoutId; tabs: string[] };
  localization: { default: string; locales: string[] };
}

/**
 * The calls the engine answers: name -> arguments and result.
 *
 * Every call takes an optional `templatesRoot`; omitting it lets the engine use
 * the templates it ships with.
 */
export interface Calls {
  'engine.info': {
    args: { templatesRoot?: string };
    result: {
      engine: string;
      engineVersion: string;
      contractVersion: string;
      protocolVersion: string;
      templatesRoot: string;
    };
  };
  'catalog.list': {
    args: { templatesRoot?: string };
    result: { features: CatalogFeature[] };
  };
  'catalog.resolve': {
    args: { features: string[]; templatesRoot?: string };
    result: { order: string[]; navCapable: string[] };
  };
  'layouts.list': {
    args: Record<string, never>;
    result: { layouts: LayoutDescriptor[] };
  };
  'locales.list': {
    args: Record<string, never>;
    result: { locales: LocaleDescriptor[]; default: string };
  };
  'vars.resolve': {
    args: { name: string; org?: string };
    result: { vars: Vars };
  };
  'workspace.create': {
    args: {
      targetDir: string;
      name: string;
      org?: string;
      features?: string[];
      layout?: LayoutId;
      tabs?: string[];
      locales?: string[];
      scaffoldPlatforms?: boolean;
      toolVersion?: string;
      templatesRoot?: string;
    };
    result: { manifest: WorkspaceManifest; env: string[]; userSteps: string[] };
  };
  'workspace.status': {
    args: { dir: string; templatesRoot?: string };
    result: {
      isWorkspace: boolean;
      manifest?: WorkspaceManifest;
      features: { id: string; summary: string; enabled: boolean; tab: boolean }[];
    };
  };
  'secrets.generate': {
    args: Record<string, never>;
    result: { secrets: Record<string, string> };
  };
}

export type CallName = keyof Calls;
export type CallArgs<K extends CallName> = Calls[K]['args'];
export type CallResult<K extends CallName> = Calls[K]['result'];

/** A JSON Schema object, as published in `tools/list`. */
export type JsonSchema = Record<string, unknown>;

export interface CallSpec {
  name: CallName;
  title: string;
  description: string;
  inputSchema: JsonSchema;
}

const templatesRootProperty = {
  templatesRoot: {
    type: 'string',
    description:
      'Explicit template-tree root. Omit to let the engine use the templates it ships with.',
  },
} as const;

const featureIds = (description: string) => ({
  type: 'array',
  items: { type: 'string' },
  description,
});

/**
 * The published surface. A client that cannot import this module discovers the
 * same thing at runtime with `tools/list`.
 */
export const CALL_SPECS: CallSpec[] = [
  {
    name: 'engine.info',
    title: 'Engine info',
    description:
      'Report the engine implementation and version, the contract version it speaks, the wire-protocol version it stamps into a workspace, and the template root it resolved.',
    inputSchema: { type: 'object', properties: { ...templatesRootProperty } },
  },
  {
    name: 'catalog.list',
    title: 'List the feature catalog',
    description:
      'List every toggleable feature, in catalog order, with the sides it covers, what it requires, and its navigation metadata when it can be surfaced as a tab.',
    inputSchema: { type: 'object', properties: { ...templatesRootProperty } },
  },
  {
    name: 'catalog.resolve',
    title: 'Resolve a feature selection',
    description:
      'Expand a requested feature set into the dependency-complete list the engine would apply, in application order, with the nav-capable subset that may be used as tabs.',
    inputSchema: {
      type: 'object',
      required: ['features'],
      properties: {
        features: featureIds('Requested feature ids.'),
        ...templatesRootProperty,
      },
    },
  },
  {
    name: 'layouts.list',
    title: 'List navigation layouts',
    description:
      'List the main-navigation layouts a workspace may be generated with, in display order.',
    inputSchema: { type: 'object', properties: {} },
  },
  {
    name: 'locales.list',
    title: 'List languages',
    description:
      'List the languages a workspace may be generated with, in catalog order, together with the default (fallback) language that is always included.',
    inputSchema: { type: 'object', properties: {} },
  },
  {
    name: 'vars.resolve',
    title: 'Resolve substitution variables',
    description:
      'Derive the substitution variables from an application name and optional organization, exactly as workspace.create would. Use it to preview a slug, package name or bundle id.',
    inputSchema: {
      type: 'object',
      required: ['name'],
      properties: {
        name: { type: 'string', description: 'Application name as typed by the user.' },
        org: { type: 'string', description: 'Reverse-DNS organization, e.g. com.acme.' },
      },
    },
  },
  {
    name: 'workspace.create',
    title: 'Create a workspace',
    description:
      'Compose a workspace (Flutter app + .NET API) at the given directory: base layers, security overlays, the requested features in dependency order, wiring, the navigation shell and the generated context docs.',
    inputSchema: {
      type: 'object',
      required: ['targetDir', 'name'],
      properties: {
        targetDir: {
          type: 'string',
          description: 'Absolute path of the workspace root to create. Must be absent or empty.',
        },
        name: {
          type: 'string',
          description: 'Application name; the substitution variables are derived from it.',
        },
        org: { type: 'string', description: 'Reverse-DNS organization. Defaults to com.<appSlug>.' },
        features: featureIds('Feature ids to enable. Dependencies are added automatically.'),
        layout: {
          enum: ['bottom_nav', 'nav_rail', 'drawer', 'home_list'],
          description: 'Main-navigation layout. Defaults to bottom_nav.',
        },
        tabs: featureIds(
          'Nav-capable enabled feature ids to surface as tabs, in tab order. Omit for every enabled nav-capable feature; an empty array yields a placeholder shell.',
        ),
        locales: {
          type: 'array',
          items: { type: 'string' },
          description:
            'Language codes to ship translations for. English is always included as the fallback. Omit for every offered language.',
        },
        scaffoldPlatforms: {
          type: 'boolean',
          description:
            'Run `flutter create` for the platform directories first. Requires the Flutter SDK on PATH. Defaults to false, which keeps the run offline.',
        },
        toolVersion: {
          type: 'string',
          description:
            "Version recorded as the workspace's generating tool. Defaults to the engine version.",
        },
        ...templatesRootProperty,
      },
    },
  },
  {
    name: 'workspace.status',
    title: 'Inspect a workspace',
    description:
      'Report whether a directory is a ctx.0 workspace and, when it is, its manifest plus which catalog features are enabled and which are navigation tabs.',
    inputSchema: {
      type: 'object',
      required: ['dir'],
      properties: {
        dir: { type: 'string', description: 'Absolute path of the directory to inspect.' },
        ...templatesRootProperty,
      },
    },
  },
  {
    name: 'secrets.generate',
    title: 'Generate server secrets',
    description:
      'Generate a fresh set of server secrets — the P-256 ALE key pair, the JWT signing key and the envelope-encryption keys — keyed by the environment variable name the generated API reads.',
    inputSchema: { type: 'object', properties: {} },
  },
];
