import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import os from 'node:os';
import path from 'node:path';
import fs from 'fs-extra';
import { createWorkspace } from '../src/compose.js';
import { resolveVars } from '../src/substitute.js';
import { loadCatalog, resolveFeatureOrder } from '../src/catalog.js';

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

  it('composes AGENTS.md as a router to per-feature docs', async () => {
    const { targetDir } = await generate(['ping', 'auth', 'notes']);
    const agents = await fs.readFile(path.join(targetDir, 'AGENTS.md'), 'utf8');

    // Preamble (from the workspace template) is preserved and substituted.
    expect(agents).toContain('# AGENTS.md — Acme');
    // Generated block is a routing table (one row per enabled feature, in order)
    // that links to each feature's dedicated doc rather than inlining its body.
    expect(agents).toContain('<!-- ctx:agents:features:start -->');
    expect(agents).toContain('<!-- ctx:agents:features:end -->');
    expect(agents).toContain('| Feature | Docs |');
    expect(agents).toContain('`docs/features/PING.md`');
    expect(agents).toContain('`docs/features/AUTH.md`');
    expect(agents).toContain('`docs/features/NOTES.md`');
    expect(agents.indexOf('docs/features/PING.md')).toBeLessThan(
      agents.indexOf('docs/features/NOTES.md'),
    );
    // The deep guidance now lives in the docs, not the root AGENTS.md.
    expect(agents).not.toContain('token_store.dart');
  });

  it('writes a dedicated doc per enabled feature and nothing more', async () => {
    const { targetDir } = await generate(['ping', 'auth', 'notes', 'notifications']);
    const docsDir = path.join(targetDir, 'docs', 'features');

    const docs = (await fs.readdir(docsDir)).sort();
    expect(docs).toEqual(['AUTH.md', 'NOTES.md', 'NOTIFICATIONS.md', 'PING.md']);

    // The doc carries the fragment body, incl. auth's merged mobile + api guidance.
    const authDoc = await fs.readFile(path.join(docsDir, 'AUTH.md'), 'utf8');
    expect(authDoc.startsWith('# auth — ')).toBe(true);
    expect(authDoc).toContain('do not hand-edit');
    expect(authDoc).toContain('token_store.dart');
    expect(authDoc).toContain('reuse detection');

    // A smaller feature set yields only its docs — no doc for a disabled feature.
    const smaller = path.join(tmp, 'smaller');
    await createWorkspace({
      targetDir: smaller,
      vars: resolveVars('Acme', 'com.acme'),
      features: ['ping', 'auth'],
    });
    const smallerDocs = (await fs.readdir(path.join(smaller, 'docs', 'features'))).sort();
    expect(smallerDocs).toEqual(['AUTH.md', 'PING.md']);

    // Fragment files are engine metadata: never copied into the workspace tree.
    const strays: string[] = [];
    const walk = async (dir: string) => {
      for (const name of await fs.readdir(dir)) {
        const abs = path.join(dir, name);
        if ((await fs.stat(abs)).isDirectory()) await walk(abs);
        else if (name === 'agents.md') strays.push(abs);
      }
    };
    await walk(targetDir);
    expect(strays).toEqual([]);
  });

  it('syncs the golden vectors into the workspace', async () => {
    const { targetDir } = await generate();
    expect(await fs.pathExists(path.join(targetDir, '.ctx', 'vectors.json'))).toBe(true);
    expect(await fs.pathExists(path.join(targetDir, '.ctx', 'wire-protocol.md'))).toBe(true);
  });

  it('records applied layers and vars in the manifest', async () => {
    const { targetDir, vars } = await generate();
    const manifest = await fs.readJson(path.join(targetDir, '.ctx', 'manifest.json'));
    expect(manifest.schema).toBe(2);
    expect(manifest.vars).toMatchObject(vars);
    // Navigation is persisted; ping is nav-capable so it defaults to a tab.
    expect(manifest.navigation.layout).toBe('bottom_nav');
    expect(manifest.navigation.tabs).toEqual(['ping']);
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

  it('scaffolds the notifications feature on both sides with idempotent wiring', async () => {
    const { targetDir } = await generate(['ping', 'auth', 'notes', 'notifications']);

    // API + mobile source landed under the right trees.
    expect(await fs.pathExists(
      path.join(targetDir, 'api', 'src', 'Api', 'Endpoints', 'NotificationsEndpoints.cs'),
    )).toBe(true);
    expect(await fs.pathExists(
      path.join(targetDir, 'api', 'src', 'Domain', 'Notifications', 'Notification.cs'),
    )).toBe(true);
    expect(await fs.pathExists(
      path.join(targetDir, 'app', 'lib', 'features', 'notifications', 'bloc', 'notifications_cubit.dart'),
    )).toBe(true);

    const program = await fs.readFile(path.join(targetDir, 'api', 'src', 'Api', 'Program.cs'), 'utf8');
    // Endpoint + services wired exactly once.
    expect(program.match(/app\.MapNotificationsEndpoints\(\);/g)?.length).toBe(1);
    expect(program).toContain('builder.Services.AddCtxNotifications(builder.Configuration);');
    // The shared `using` is not duplicated even though notes + notifications both wire it.
    expect(program.match(/using Acme\.Api\.Endpoints;/g)?.length).toBe(1);

    // Firebase deps injected into pubspec via the shared anchor.
    const pubspec = await fs.readFile(path.join(targetDir, 'app', 'pubspec.yaml'), 'utf8');
    expect(pubspec).toContain('firebase_messaging:');

    // Both entity configurations registered on the DbContext.
    const dbContext = await fs.readFile(
      path.join(targetDir, 'api', 'src', 'Infrastructure', 'Persistence', 'AcmeDbContext.cs'),
      'utf8',
    );
    expect(dbContext).toContain('NotificationConfiguration');
    expect(dbContext).toContain('DeviceTokenConfiguration');

    // Manifest records both sides.
    const manifest = await fs.readJson(path.join(targetDir, '.ctx', 'manifest.json'));
    const ids = manifest.features.map((f: { id: string }) => f.id);
    expect(ids).toContain('notifications:mobile');
    expect(ids).toContain('notifications:api');
  });

  it('generates a bottom-nav shell with a destination + page per tab', async () => {
    const { targetDir } = await generate(['ping']);
    const shell = await fs.readFile(path.join(targetDir, 'app', 'lib', 'app', 'shell.dart'), 'utf8');
    expect(shell).toContain('NavigationBar');
    expect(shell).toContain("NavigationDestination(icon: Icon(Icons.lock), label: 'Secure ping')");
    expect(shell).toContain('PingPage()');
    expect(shell).toContain("import '../features/ping/views/ping_page.dart';");
    // The base shell no longer ships a static home page.
    expect(await fs.pathExists(path.join(targetDir, 'app', 'lib', 'app', 'home_page.dart'))).toBe(false);
    // app.dart renders the generated shell.
    const app = await fs.readFile(path.join(targetDir, 'app', 'lib', 'app', 'app.dart'), 'utf8');
    expect(app).toContain('const CtxShell()');
  });

  it('honours the chosen layout and tab set', async () => {
    const vars = resolveVars('Acme', 'com.acme');
    const targetDir = path.join(tmp, 'drawerws');
    await createWorkspace({
      targetDir,
      vars,
      features: ['ping', 'auth', 'notifications'],
      layout: 'drawer',
      tabs: ['notifications'],
    });
    const shell = await fs.readFile(path.join(targetDir, 'app', 'lib', 'app', 'shell.dart'), 'utf8');
    expect(shell).toContain('NavigationDrawer');
    expect(shell).toContain('NavigationDrawerDestination');
    expect(shell).toContain('NotificationsPage()');
    // ping was enabled but not chosen as a tab: it is not in the shell.
    expect(shell).not.toContain('PingPage');
    const manifest = await fs.readJson(path.join(targetDir, '.ctx', 'manifest.json'));
    expect(manifest.navigation).toEqual({ layout: 'drawer', tabs: ['notifications'] });
  });

  it('renders a placeholder shell when no tabs are selected', async () => {
    const vars = resolveVars('Acme', 'com.acme');
    const targetDir = path.join(tmp, 'emptyws');
    await createWorkspace({ targetDir, vars, features: [], tabs: [] });
    const shell = await fs.readFile(path.join(targetDir, 'app', 'lib', 'app', 'shell.dart'), 'utf8');
    expect(shell).toContain('class CtxShell');
    expect(shell).toContain('placeholder');
    expect(shell).not.toContain('NavigationBar');
    expect(shell).not.toContain('../features/');
  });

  it('rejects a nav tab that is not an enabled feature', async () => {
    const vars = resolveVars('Acme', 'com.acme');
    const targetDir = path.join(tmp, 'badtabs');
    await expect(
      createWorkspace({ targetDir, vars, features: ['auth'], tabs: ['ping'] }),
    ).rejects.toThrow(/not an enabled feature/);
  });

  it('refuses a non-empty target directory', async () => {
    const { targetDir, vars } = await generate();
    await expect(
      createWorkspace({ targetDir, vars, features: ['auth'] }),
    ).rejects.toThrow(/not empty/);
  });
});
