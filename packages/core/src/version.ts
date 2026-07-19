import { fileURLToPath } from 'node:url';
import path from 'node:path';
import fs from 'fs-extra';
import { templateLayout } from './paths.js';

/** The default wire-protocol version, used when `protocol.json` declares none. */
const DEFAULT_PROTOCOL_VERSION = '1.0';

/**
 * The wire-protocol version shared by both sides of a generated workspace, read
 * from `protocol/protocol.json`. Stamped into the workspace manifest.
 */
export function protocolVersion(explicitRoot?: string): string {
  const p = path.join(templateLayout(explicitRoot).protocol, 'protocol.json');
  if (fs.existsSync(p)) {
    const j = fs.readJsonSync(p) as { version?: string };
    if (j.version) return j.version;
  }
  return DEFAULT_PROTOCOL_VERSION;
}

/**
 * The `@ctx0/core` engine version, read from this package's own package.json.
 *
 * It is used as the fallback for the `ctx0Version` stamped into a generated
 * workspace manifest. A frontend (the `ctx0` CLI, ctx.0-MCP, …) should pass its
 * own tool version via `CreateOptions.toolVersion` so the manifest records the
 * tool that generated the workspace; when omitted, this engine version is used.
 */
export function coreVersion(): string {
  const here = path.dirname(fileURLToPath(import.meta.url)); // dist (or src under tsx/vitest)
  const pkgPath = path.resolve(here, '..', 'package.json'); // packages/core/package.json
  try {
    const pkg = fs.readJsonSync(pkgPath) as { version?: string };
    return pkg.version ?? '0.0.0';
  } catch {
    return '0.0.0';
  }
}
