import { fileURLToPath } from 'node:url';
import path from 'node:path';
import fs from 'fs-extra';

/** The ctx.0 CLI version, read from the package's own package.json. */
export function cliVersion(): string {
  const here = path.dirname(fileURLToPath(import.meta.url)); // dist/engine
  const pkgPath = path.resolve(here, '..', '..', 'package.json'); // packages/cli/package.json
  try {
    const pkg = fs.readJsonSync(pkgPath) as { version?: string };
    return pkg.version ?? '0.0.0';
  } catch {
    return '0.0.0';
  }
}
