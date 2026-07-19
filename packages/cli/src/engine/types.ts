/**
 * Core types for the ctx.0 composition engine.
 *
 * A generated workspace is produced by copying a `base` template tree and then
 * layering one self-contained *overlay* directory per enabled feature on top.
 * Every overlay (and each `base`) is described by a `feature.json` manifest.
 */

export type Side = 'mobile' | 'api';

/** A single idempotent edit to a shared/base file (e.g. Program.cs, pubspec.yaml). */
export interface WiringEdit {
  /** Workspace-relative path of the file to edit, e.g. "api/src/Api/Program.cs". */
  file: string;
  /**
   * Anchor tag to insert near. The target file must contain a line with the
   * marker `// ctx:anchor:<anchor>` (or `# ctx:anchor:<anchor>` / `<!-- -->`).
   * The `insert` text is placed immediately below that marker line.
   */
  anchor: string;
  /** Text to insert. Substitution tokens are applied before insertion. */
  insert: string;
}

/** Dependency additions merged into the target project's manifest. */
export interface FeatureDeps {
  /** pubspec.yaml dependencies to add for the mobile side: name -> version constraint. */
  pubspec?: Record<string, string>;
  /** NuGet PackageReference additions for the api side: package id -> version. */
  nuget?: Record<string, string>;
}

/** A feature manifest (`feature.json`) shipped alongside each overlay. */
export interface FeatureManifest {
  /** Stable feature id, e.g. "auth", "payments_stripe". Matches the overlay dir name. */
  id: string;
  /** Human-readable one-line summary. */
  summary: string;
  /** Which trees this feature touches. */
  sides: Side[];
  /** Feature ids that must be enabled first (resolved transitively). */
  requires?: string[];
  /** True for always-on parts of the workspace (base, security). Cannot be disabled. */
  core?: boolean;
  /** Whether the feature contributes a bottom-nav / rail tab in the mobile shell. */
  providesNavTab?: boolean;
  /** Dependency additions per side. */
  deps?: FeatureDeps;
  /** Idempotent edits to shared files. */
  wiring?: WiringEdit[];
  /** Environment variables the consumer must set for this feature to work. */
  env?: string[];
  /** Post-generation manual steps surfaced to the user. */
  userSteps?: string[];
}

/** Record of one applied feature, persisted in the workspace manifest. */
export interface AppliedFeature {
  id: string;
  /** Files this overlay wrote (workspace-relative), for clean removal on disable. */
  files: string[];
  /** Content hash of the overlay source that was applied (integrity / drift check). */
  hash: string;
}

/** The `.ctx/manifest.json` persisted at the root of a generated workspace. */
export interface WorkspaceManifest {
  /** Schema version of the manifest itself. */
  schema: 1;
  /** CLI version that generated / last modified the workspace. */
  ctx0Version: string;
  /** Wire-protocol version shared by both sides. */
  protocolVersion: string;
  /** The substitution variables chosen at create time. */
  vars: TemplateVars;
  /** Enabled features in application order (base + security first). */
  features: AppliedFeature[];
}

/** Substitution variables resolved once per workspace. */
export interface TemplateVars {
  /** PascalCase application/type name, e.g. "Acme". Replaces the `CtxApp` token. */
  appName: string;
  /** snake/lower application slug, e.g. "acme". Replaces the `ctxapp` token. */
  appSlug: string;
  /** Reverse-DNS org, e.g. "com.acme". Replaces the `com.ctx.app` org token base. */
  org: string;
  /** Bundle/application id, e.g. "com.acme.app". Replaces `com.ctx.app`. */
  bundleId: string;
}
