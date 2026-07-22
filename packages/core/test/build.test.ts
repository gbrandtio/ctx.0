import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import os from 'node:os';
import path from 'node:path';
import { execFile, execFileSync } from 'node:child_process';
import { promisify } from 'node:util';
import fs from 'fs-extra';
import { createWorkspace } from '../src/compose.js';
import { resolveVars } from '../src/substitute.js';
import { loadCatalog } from '../src/catalog.js';

const execFileAsync = promisify(execFile);

/**
 * The generator's other tests prove files land at the right paths and that wiring
 * inserts at the right anchors — but they never compile the emitted C#, so a
 * template that does not build passes them all. This test closes that gap: it
 * generates a workspace with every API feature and runs `dotnet build` against it,
 * so a template that does not compile fails the suite.
 *
 * It is skipped when the .NET SDK is not installed, so local runs without the SDK
 * still pass; CI must provide the SDK for the guard to run.
 */
function hasDotnetSdk(): boolean {
  try {
    execFileSync('dotnet', ['--version'], { stdio: 'ignore' });
    return true;
  } catch {
    return false;
  }
}

const dotnetAvailable = hasDotnetSdk();

let tmp: string;

beforeEach(async () => {
  tmp = await fs.mkdtemp(path.join(os.tmpdir(), 'ctx0-build-'));
});

afterEach(async () => {
  await fs.remove(tmp);
});

describe.skipIf(!dotnetAvailable)('generated API compiles', () => {
  it(
    'dotnet build succeeds for a workspace with every API feature',
    async () => {
      const vars = resolveVars('Acme', 'com.acme');
      const targetDir = path.join(tmp, vars.appSlug);

      // Every toggleable feature that ships an API side; createWorkspace resolves
      // their dependencies. Derived from the catalog so new features are covered
      // automatically.
      const catalog = loadCatalog();
      const apiFeatures = [...catalog.values()]
        .filter((entry) => entry.manifest.sides.includes('api'))
        .map((entry) => entry.manifest.id)
        .sort();

      await createWorkspace({ targetDir, vars, features: apiFeatures });

      const apiDir = path.join(targetDir, 'api');
      let output: string;
      try {
        const result = await execFileAsync('dotnet', ['build', '--nologo', '-clp:ErrorsOnly'], {
          cwd: apiDir,
          maxBuffer: 64 * 1024 * 1024,
        });
        output = `${result.stdout}\n${result.stderr}`;
      } catch (error) {
        const err = error as { stdout?: string; stderr?: string };
        throw new Error(`dotnet build failed:\n${err.stdout ?? ''}\n${err.stderr ?? ''}`);
      }

      expect(output).toContain('Build succeeded');
    },
    600_000,
  );
});
