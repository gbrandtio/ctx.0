import path from 'node:path';
import os from 'node:os';
import fs from 'fs-extra';
import { afterAll, beforeAll, describe, expect, it } from 'vitest';
import { CALL_SPECS } from '../src/contract.js';
import { HANDLERS, dispatch } from '../src/tools.js';

let tmp: string;
beforeAll(async () => {
  tmp = await fs.mkdtemp(path.join(os.tmpdir(), 'ctx0-engine-'));
});
afterAll(async () => {
  await fs.remove(tmp);
});

describe('contract coverage', () => {
  it('implements exactly the declared calls', () => {
    expect(Object.keys(HANDLERS).sort()).toEqual(CALL_SPECS.map((c) => c.name).sort());
  });

  it('rejects an unknown call', async () => {
    await expect(dispatch('no.such.call', {})).rejects.toThrow(/Unknown call/);
  });

  it('rejects arguments the contract declares required', async () => {
    await expect(dispatch('vars.resolve', {})).rejects.toThrow(/"name" is required/);
    await expect(dispatch('catalog.resolve', { features: 'ping' })).rejects.toThrow(
      /"features" is required/,
    );
  });
});

describe('calls', () => {
  it('engine.info reports the versions a client checks', async () => {
    const info = (await dispatch('engine.info', {})) as Record<string, string>;

    expect(info.engine).toBe('@ctx0/core');
    expect(info.contractVersion).toBe('2');
    expect(await fs.pathExists(path.join(info.templatesRoot!, 'workspace'))).toBe(true);
  });

  it('catalog.list reports features with their navigation metadata', async () => {
    const { features } = (await dispatch('catalog.list', {})) as {
      features: { id: string; sides: string[]; nav?: { label: string } }[];
    };

    const ping = features.find((f) => f.id === 'ping');
    expect(ping?.sides.length).toBeGreaterThan(0);
    expect(ping?.nav?.label).toBeTruthy();
  });

  it('catalog.resolve adds dependencies in application order', async () => {
    const { order, navCapable } = (await dispatch('catalog.resolve', {
      features: ['profile'],
    })) as { order: string[]; navCapable: string[] };

    expect(order.indexOf('auth')).toBeLessThan(order.indexOf('profile'));
    expect(navCapable.every((id) => order.includes(id))).toBe(true);
  });

  it('catalog.resolve rejects an unknown feature', async () => {
    await expect(dispatch('catalog.resolve', { features: ['no_such_feature'] })).rejects.toThrow();
  });

  it('vars.resolve derives the substitution variables', async () => {
    const { vars } = (await dispatch('vars.resolve', { name: 'My App', org: 'com.Example' })) as {
      vars: Record<string, string>;
    };

    expect(vars).toEqual({
      appName: 'MyApp',
      appSlug: 'my_app',
      org: 'com.example',
      bundleId: 'com.example.my_app',
    });
  });

  it('workspace.create composes a workspace that workspace.status reads back', async () => {
    const targetDir = path.join(tmp, 'demo');
    const created = (await dispatch('workspace.create', {
      targetDir,
      name: 'Demo',
      org: 'com.demo',
      features: ['ping'],
    })) as { manifest: { vars: Record<string, string> } };

    expect(created.manifest.vars.appSlug).toBe('demo');
    expect(await fs.pathExists(path.join(targetDir, '.ctx', 'manifest.json'))).toBe(true);

    const status = (await dispatch('workspace.status', { dir: targetDir })) as {
      isWorkspace: boolean;
      features: { id: string; enabled: boolean }[];
    };

    expect(status.isWorkspace).toBe(true);
    expect(status.features.find((f) => f.id === 'ping')?.enabled).toBe(true);
    expect(status.features.find((f) => f.id === 'media')?.enabled).toBe(false);
  });

  it('workspace.status reports a plain directory as not a workspace', async () => {
    const dir = path.join(tmp, 'empty');
    await fs.ensureDir(dir);

    const status = (await dispatch('workspace.status', { dir })) as { isWorkspace: boolean };
    expect(status.isWorkspace).toBe(false);
  });

  it('workspace.create rejects a relative target directory', async () => {
    await expect(
      dispatch('workspace.create', { targetDir: 'relative/path', name: 'Demo' }),
    ).rejects.toThrow(/absolute/);
  });

  it('secrets.generate produces correctly encoded key material', async () => {
    const { secrets } = (await dispatch('secrets.generate', {})) as {
      secrets: Record<string, string>;
    };

    expect(Buffer.from(secrets.Ctx__Ale__PrivateKey!, 'base64')).toHaveLength(32);
    const pub = Buffer.from(secrets.Ctx__Ale__PublicKey!, 'base64');
    expect(pub).toHaveLength(65);
    expect(pub[0]).toBe(0x04);
    expect(Buffer.from(secrets.Ctx__Jwt__SigningKey!, 'base64')).toHaveLength(48);
    expect(Buffer.from(secrets.Ctx__Envelope__Keks__1!, 'base64')).toHaveLength(32);
    expect(secrets.Ctx__Envelope__ActiveKekVersion).toBe('1');
    expect(Buffer.from(secrets.Ctx__Envelope__BlindIndexKey!, 'base64')).toHaveLength(32);
  });
});
