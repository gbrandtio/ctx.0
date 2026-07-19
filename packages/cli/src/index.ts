#!/usr/bin/env node
import { Command } from 'commander';
import pc from 'picocolors';
import { cliVersion } from './version.js';
import { runCreate } from './commands/create.js';
import { runStatus } from './commands/status.js';
import { runKeygen } from './commands/keygen.js';

const program = new Command();

program
  .name('ctx0')
  .description('Security-first scaffolder for a Flutter app + .NET API')
  .version(cliVersion(), '-v, --version');

const create = program.command('create').description('Scaffold a new project');

create
  .command('workspace <name>')
  .description('Create a full workspace (Flutter app + .NET API)')
  .option('-o, --org <org>', 'reverse-DNS organization, e.g. com.acme')
  .option('-d, --dir <dir>', 'parent directory to create the workspace in')
  .option('-f, --features <ids...>', 'feature ids to enable (default: auth)')
  .option('--no-platforms', 'skip `flutter create`; generate the ctx.0 source overlay only')
  .action(
    async (name: string, opts: { org?: string; dir?: string; features?: string[]; platforms?: boolean }) => {
      await runCreate({
        name,
        org: opts.org,
        dir: opts.dir,
        features: opts.features,
        platforms: opts.platforms,
      });
    },
  );

program
  .command('keygen')
  .description('Generate a server ALE key pair (P-256) as environment variables')
  .action(() => {
    runKeygen();
  });

program
  .command('status')
  .description('List the feature catalog, or show enabled features inside a workspace')
  .action(async () => {
    await runStatus();
  });

async function main() {
  try {
    await program.parseAsync(process.argv);
  } catch (err) {
    const message = err instanceof Error ? err.message : String(err);
    console.error(`\n${pc.red('✗')} ${message}\n`);
    process.exitCode = 1;
  }
}

void main();
