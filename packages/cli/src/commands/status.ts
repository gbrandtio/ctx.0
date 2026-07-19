import pc from 'picocolors';
import { loadCatalog, isWorkspace, readManifest } from '@ctx0/core';

/**
 * `ctx0 status` — outside a workspace, list the toggleable feature catalog;
 * inside a workspace, show which features are enabled.
 */
export async function runStatus(): Promise<void> {
  const cwd = process.cwd();
  const catalog = loadCatalog();

  if (await isWorkspace(cwd)) {
    const manifest = await readManifest(cwd);
    const enabled = new Set(
      manifest.features
        .map((f) => f.id.split(':')[0] ?? f.id)
        .filter((id) => catalog.has(id)),
    );
    console.log(pc.bold(`\nWorkspace ${pc.cyan(manifest.vars.appName)} — protocol v${manifest.protocolVersion}\n`));
    const tabs = new Set(manifest.navigation?.tabs ?? []);
    for (const [id, entry] of catalog) {
      const on = enabled.has(id);
      const mark = on ? pc.green('●') : pc.dim('○');
      const tab = tabs.has(id) ? pc.cyan(' [tab]') : '';
      console.log(`  ${mark} ${id.padEnd(20)} ${pc.dim(entry.manifest.summary)}${tab}`);
    }
    if (manifest.navigation) {
      console.log(pc.dim(`\n  layout: ${manifest.navigation.layout}`));
    }
    console.log();
    return;
  }

  console.log(pc.bold('\nAvailable features:\n'));
  for (const [id, entry] of catalog) {
    const sides = entry.manifest.sides.join('+');
    console.log(`  ${id.padEnd(20)} ${pc.dim(`[${sides}]`)} ${entry.manifest.summary}`);
  }
  console.log(pc.dim('\nRun inside a workspace to see what is enabled.\n'));
}
