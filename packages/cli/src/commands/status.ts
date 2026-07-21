import pc from 'picocolors';
import { withEngine } from '../engine.js';

/**
 * `ctx0 status` — outside a workspace, list the toggleable feature catalog;
 * inside a workspace, show which features are enabled.
 */
export async function runStatus(): Promise<void> {
  const cwd = process.cwd();

  await withEngine(async (engine) => {
    const status = await engine.call('workspace.status', { dir: cwd });

    if (status.isWorkspace && status.manifest) {
      const { manifest } = status;
      console.log(
        pc.bold(
          `\nWorkspace ${pc.cyan(manifest.vars.appName)} — protocol v${manifest.protocolVersion}\n`,
        ),
      );
      for (const feature of status.features) {
        const mark = feature.enabled ? pc.green('●') : pc.dim('○');
        const tab = feature.tab ? pc.cyan(' [tab]') : '';
        console.log(`  ${mark} ${feature.id.padEnd(20)} ${pc.dim(feature.summary)}${tab}`);
      }
      console.log(pc.dim(`\n  layout   : ${manifest.navigation.layout}`));
      console.log(pc.dim(`  languages: ${manifest.localization.locales.join(', ')}`));
      console.log();
      return;
    }

    const { features } = await engine.call('catalog.list', {});
    console.log(pc.bold('\nAvailable features:\n'));
    for (const feature of features) {
      console.log(
        `  ${feature.id.padEnd(20)} ${pc.dim(`[${feature.sides.join('+')}]`)} ${feature.summary}`,
      );
    }
    console.log(pc.dim('\nRun inside a workspace to see what is enabled.\n'));
  });
}
