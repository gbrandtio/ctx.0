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
    // l10n is pulled in as a dependency of every feature that carries strings,
    // and l10n in turn pulls in settings (the hub its Language row lives under).
    expect(docs).toEqual([
      'AUTH.md',
      'L10N.md',
      'NOTES.md',
      'NOTIFICATIONS.md',
      'PING.md',
      'SETTINGS.md',
    ]);

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
    expect(smallerDocs).toEqual(['AUTH.md', 'L10N.md', 'PING.md', 'SETTINGS.md']);

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

  it('generates the theme the base app imports, without a font by default', async () => {
    const { targetDir } = await generate();

    const theme = await fs.readFile(path.join(targetDir, 'app', 'lib', 'app', 'theme.dart'), 'utf8');
    expect(theme).toContain('static const Color seed = Color(0xFF3F51B5);');
    expect(theme).not.toContain('google_fonts');

    const app = await fs.readFile(path.join(targetDir, 'app', 'lib', 'app', 'app.dart'), 'utf8');
    expect(app).toContain('theme: AppTheme.light()');
    expect(app).toContain('darkTheme: AppTheme.dark()');

    // No font chosen, so the app gains no font package.
    const pubspec = await fs.readFile(path.join(targetDir, 'app', 'pubspec.yaml'), 'utf8');
    expect(pubspec).not.toContain('google_fonts');
  });

  it('adds google_fonts only when a font is chosen', async () => {
    const vars = resolveVars('Acme', 'com.acme');
    const targetDir = path.join(tmp, 'themed');
    const result = await createWorkspace({
      targetDir,
      vars,
      features: ['ping'],
      scheme: 'teal',
      font: 'inter',
    });

    const pubspec = await fs.readFile(path.join(targetDir, 'app', 'pubspec.yaml'), 'utf8');
    expect(pubspec).toContain('google_fonts:');

    const theme = await fs.readFile(path.join(targetDir, 'app', 'lib', 'app', 'theme.dart'), 'utf8');
    expect(theme).toContain('static const Color seed = Color(0xFF009688);');
    expect(theme).toContain('GoogleFonts.interTextTheme(base.textTheme)');

    expect(result.manifest.theme).toEqual({ scheme: 'teal', font: 'inter' });
  });

  it('records applied layers and vars in the manifest', async () => {
    const { targetDir, vars } = await generate();
    const manifest = await fs.readJson(path.join(targetDir, '.ctx', 'manifest.json'));
    expect(manifest.schema).toBe(5);
    expect(manifest.vars).toMatchObject(vars);
    // Navigation is persisted; ping is nav-capable so it defaults to a tab, while
    // the l10n feature it pulls in is now a Settings row (not a tab), and l10n in
    // turn pulls in the settings hub.
    expect(manifest.navigation.layout).toBe('bottom_nav');
    expect(manifest.navigation.tabs).toEqual(['ping']);
    expect(manifest.navigation.settings).toEqual(['l10n']);
    // The languages are persisted too: every offered one, unless narrowed.
    expect(manifest.localization.default).toBe('en');
    expect(manifest.localization.locales).toEqual(['en', 'el', 'de', 'fr', 'es']);
    // The theme is persisted as what was asked for: the default scheme, and no
    // font at all rather than a name standing in for the platform font.
    expect(manifest.theme.scheme).toBe('indigo');
    expect(manifest.theme.font).toBeUndefined();
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

  it('scaffolds the gdpr feature and layers its banner above the auth gate', async () => {
    const { targetDir } = await generate(['auth', 'notes', 'gdpr']);

    expect(await fs.pathExists(
      path.join(targetDir, 'api', 'src', 'Api', 'Endpoints', 'PrivacyEndpoints.cs'),
    )).toBe(true);
    expect(await fs.pathExists(
      path.join(targetDir, 'app', 'lib', 'features', 'gdpr', 'views', 'consent_banner.dart'),
    )).toBe(true);

    const program = await fs.readFile(path.join(targetDir, 'api', 'src', 'Api', 'Program.cs'), 'utf8');
    expect(program.match(/app\.MapPrivacyEndpoints\(\);/g)?.length).toBe(1);
    expect(program.match(/builder\.Services\.AddCtxGdpr\(builder\.Configuration\);/g)?.length).toBe(1);
    // Every data-owning feature registers its personal-data contributor.
    expect(program).toContain('Acme.Infrastructure.Gdpr.AuthPersonalData');
    expect(program).toContain('Acme.Infrastructure.Gdpr.NotesPersonalData');

    // The banner wraps every route (app-overlay), so it shows before the auth
    // gate's login screen — which wraps only the home route (home-wrap).
    const app = await fs.readFile(path.join(targetDir, 'app', 'lib', 'app', 'app.dart'), 'utf8');
    expect(app).toContain('content = ConsentBanner(child: content);');
    expect(app).toContain('home = AuthGate(child: home);');

    const dbContext = await fs.readFile(
      path.join(targetDir, 'api', 'src', 'Infrastructure', 'Persistence', 'AcmeDbContext.cs'),
      'utf8',
    );
    expect(dbContext).toContain('ConsentRecordConfiguration');
    expect(dbContext).toContain('DataExportJobConfiguration');
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
    // l10n (pulled in by every string-carrying feature) is a Settings row, so it
    // is recorded under settings rather than as a tab.
    expect(manifest.navigation).toEqual({
      layout: 'drawer',
      tabs: ['notifications'],
      settings: ['l10n'],
    });
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

describe('session layer', () => {
  it('is applied even with no features and owns the credential store', async () => {
    const { targetDir, result } = await generate([]);

    expect(result.manifest.features.map((f) => f.id)).toContain('session');
    expect(
      await fs.pathExists(path.join(targetDir, 'app', 'lib', 'session', 'token_store.dart')),
    ).toBe(true);
    expect(
      await fs.pathExists(path.join(targetDir, 'app', 'lib', 'session', 'session_cubit.dart')),
    ).toBe(true);

    // The session provider and the locale wiring are always present.
    const di = await fs.readFile(path.join(targetDir, 'app', 'lib', 'app', 'di.dart'), 'utf8');
    expect(di).toContain('BlocProvider<SessionCubit>');
    expect(di).toContain('BlocProvider<LocaleCubit>');
    const app = await fs.readFile(path.join(targetDir, 'app', 'lib', 'app', 'app.dart'), 'utf8');
    expect(app).toContain('locale: context.watch<LocaleCubit>().state,');
  });

  it('makes auth a session provider: consumers read ctxSession, not the auth store', async () => {
    const { targetDir } = await generate(['auth', 'profile']);

    const di = await fs.readFile(path.join(targetDir, 'app', 'lib', 'app', 'di.dart'), 'utf8');
    // Exactly one SessionCubit registration (from the session layer, not auth).
    expect(di.match(/BlocProvider<SessionCubit>/g)?.length).toBe(1);
    expect(di).toContain('HttpProfileRepository(ctxSession)');
    expect(di).not.toContain('ctxTokens');

    // Nothing imports the old auth-owned token store; profile reads the session.
    const profileRepo = await fs.readFile(
      path.join(targetDir, 'app', 'lib', 'features', 'profile', 'data', 'profile_repository.dart'),
      'utf8',
    );
    expect(profileRepo).toContain("import 'package:acme/session/token_store.dart';");
    expect(
      await fs.pathExists(
        path.join(targetDir, 'app', 'lib', 'features', 'auth', 'data', 'token_store.dart'),
      ),
    ).toBe(false);
  });
});
