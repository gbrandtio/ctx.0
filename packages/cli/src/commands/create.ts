import path from 'node:path';
import fs from 'fs-extra';
import pc from 'picocolors';
import { createWorkspace } from '../engine/compose.js';
import { resolveVars } from '../engine/substitute.js';

export interface CreateArgs {
  name: string;
  org?: string;
  dir?: string;
  features?: string[];
  /** Generate the Flutter platform scaffolding via `flutter create` (default true). */
  platforms?: boolean;
}

/** Default feature set for `create workspace`: ping + auth + encrypted/RLS notes. */
const DEFAULT_FEATURES = ['ping', 'auth', 'notes'];

export async function runCreate(args: CreateArgs): Promise<void> {
  const vars = resolveVars(args.name, args.org);
  const targetDir = path.resolve(args.dir ?? process.cwd(), vars.appSlug);

  console.log(pc.bold(`\nCreating ctx.0 workspace ${pc.cyan(vars.appName)}`));
  console.log(`  location : ${pc.dim(targetDir)}`);
  console.log(`  bundle   : ${pc.dim(vars.bundleId)}`);

  const features = args.features?.length ? args.features : DEFAULT_FEATURES;
  console.log(`  features : ${pc.dim(features.join(', ') || '(none)')}\n`);

  const scaffoldPlatforms = args.platforms !== false;
  if (scaffoldPlatforms) {
    console.log(pc.dim('  Running `flutter create` for the app/ platform scaffolding…'));
  }

  const result = await createWorkspace({ targetDir, vars, features, scaffoldPlatforms });

  console.log(pc.green('✓ Workspace generated.'));
  console.log(`  app/  Flutter (Bloc)   api/  .NET (Clean Architecture)`);
  console.log(`  ${result.manifest.features.length} layers, protocol v${result.manifest.protocolVersion}\n`);

  if (result.env.length) {
    console.log(pc.bold('Environment variables to set:'));
    for (const e of result.env) console.log(`  - ${e}`);
    console.log();
  }
  if (result.userSteps.length) {
    console.log(pc.bold('Next steps:'));
    for (const s of result.userSteps) console.log(`  - ${s}`);
    console.log();
  }

  console.log(pc.dim(`cd ${path.relative(process.cwd(), targetDir)} && cat README.md`));
  await ensureReadmeHint(targetDir);
}

async function ensureReadmeHint(dir: string): Promise<void> {
  // Non-fatal: only warn if the workspace template forgot a README.
  if (!(await fs.pathExists(path.join(dir, 'README.md')))) {
    console.log(pc.yellow('  (note: generated workspace has no README.md)'));
  }
}
