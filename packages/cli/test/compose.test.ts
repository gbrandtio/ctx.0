import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import os from 'node:os';
import path from 'node:path';
import fs from 'fs-extra';
import { createWorkspace } from '../src/engine/compose.js';
import { resolveVars } from '../src/engine/substitute.js';
import { loadCatalog, resolveFeatureOrder } from '../src/engine/catalog.js';

let tmp: string;

beforeEach(async () => {
  tmp = await fs.mkdtemp(path.join(os.tmpdir(), 'ctx0-test-'));
});

afterEach(async () => {
  await fs.remove(tmp);
});

async function generate(features: string[] = ['ping']) {
  const vars = resolveVars('Acme', 'com.acme');
  const targetDir = path.join(tmp, vars.appSlug);
  const result = await createWorkspace({ targetDir, vars, features });
  return { targetDir, result, vars };
}

describe('catalog', () => {
  it('discovers the ping feature on both sides', () => {
    const catalog = loadCatalog();
    const ping = catalog.get('ping');
    expect(ping).toBeDefined();
    expect(ping!.manifest.sides.sort()).toEqual(['api', 'mobile']);
    expect(ping!.dirs.mobile).toBeTruthy();
    expect(ping!.dirs.api).toBeTruthy();
  });

  it('rejects unknown features', () => {
    const catalog = loadCatalog();
    expect(() => resolveFeatureOrder(['does_not_exist'], catalog)).toThrow(/Unknown feature/);
  });
});

describe('createWorkspace', () => {
  it('composes base + security + auth and substitutes tokens', async () => {
    const { targetDir } = await generate();

    // Structure exists.
    expect(await fs.pathExists(path.join(targetDir, 'app', 'pubspec.yaml'))).toBe(true);
    expect(await fs.pathExists(path.join(targetDir, 'api', 'CtxApp.sln'.replace('CtxApp', 'Acme')))).toBe(true);
    expect(await fs.pathExists(path.join(targetDir, '.ctx', 'manifest.json'))).toBe(true);

    // Substitution applied in content.
    const pubspec = await fs.readFile(path.join(targetDir, 'app', 'pubspec.yaml'), 'utf8');
    expect(pubspec).toContain('name: acme');
    expect(pubspec).not.toContain('ctxapp');

    const program = await fs.readFile(path.join(targetDir, 'api', 'src', 'Api', 'Program.cs'), 'utf8');
    expect(program).toContain('Acme.Infrastructure.Persistence');
    expect(program).not.toContain('CtxApp');
  });

  it('applies wiring idempotently for enabled features', async () => {
    const { targetDir } = await generate();

    const program = await fs.readFile(path.join(targetDir, 'api', 'src', 'Api', 'Program.cs'), 'utf8');
    expect(program).toContain('app.MapPingEndpoints();');
    // Inserted exactly once, below the anchor.
    expect(program.match(/app\.MapPingEndpoints\(\);/g)?.length).toBe(1);

    const di = await fs.readFile(path.join(targetDir, 'app', 'lib', 'app', 'di.dart'), 'utf8');
    expect(di).toContain('BlocProvider<PingCubit>');
    expect(di).toContain("import '../features/ping/ping_cubit.dart';");
  });

  it('substitutes tokens in wiring target paths (renamed DbContext)', async () => {
    // The auth feature wires into CtxAppDbContext.cs, which is renamed by
    // substitution; the wiring must follow the renamed file.
    const { targetDir } = await generate(['auth']);
    const dbContext = await fs.readFile(
      path.join(targetDir, 'api', 'src', 'Infrastructure', 'Persistence', 'AcmeDbContext.cs'),
      'utf8',
    );
    expect(dbContext).toContain('RefreshTokenConfiguration');
    expect(dbContext).toContain('UserCredentialConfiguration');
  });

  it('syncs the golden vectors into the workspace', async () => {
    const { targetDir } = await generate();
    expect(await fs.pathExists(path.join(targetDir, '.ctx', 'vectors.json'))).toBe(true);
    expect(await fs.pathExists(path.join(targetDir, '.ctx', 'wire-protocol.md'))).toBe(true);
  });

  it('records applied layers and vars in the manifest', async () => {
    const { targetDir, vars } = await generate();
    const manifest = await fs.readJson(path.join(targetDir, '.ctx', 'manifest.json'));
    expect(manifest.schema).toBe(1);
    expect(manifest.vars).toMatchObject(vars);
    const ids = manifest.features.map((f: { id: string }) => f.id);
    expect(ids).toContain('security_mobile');
    expect(ids).toContain('security_api');
    expect(ids).toContain('ping:mobile');
    expect(ids).toContain('ping:api');
    // Every layer records a hash and file list.
    for (const f of manifest.features) {
      expect(typeof f.hash).toBe('string');
      expect(Array.isArray(f.files)).toBe(true);
    }
  });

  it('refuses a non-empty target directory', async () => {
    const { targetDir, vars } = await generate();
    await expect(
      createWorkspace({ targetDir, vars, features: ['auth'] }),
    ).rejects.toThrow(/not empty/);
  });
});
