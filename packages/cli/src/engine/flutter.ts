import { spawn } from 'node:child_process';
import path from 'node:path';
import fs from 'fs-extra';
import type { TemplateVars } from './types.js';

/** Run a command to completion, resolving on exit code 0 and rejecting otherwise. */
function run(
  command: string,
  args: string[],
  opts: { cwd?: string; stdio?: 'inherit' | 'pipe' } = {},
): Promise<{ code: number; stderr: string }> {
  return new Promise((resolve, reject) => {
    const child = spawn(command, args, {
      cwd: opts.cwd,
      stdio: opts.stdio ?? 'inherit',
      shell: false,
    });
    let stderr = '';
    if (opts.stdio === 'pipe') {
      child.stderr?.on('data', (d) => (stderr += String(d)));
    }
    child.on('error', reject);
    child.on('close', (code) => resolve({ code: code ?? 1, stderr }));
  });
}

/** Throw a clear, actionable error if the Flutter SDK is not on PATH. */
export async function ensureFlutterAvailable(): Promise<void> {
  try {
    const { code } = await run('flutter', ['--version'], { stdio: 'pipe' });
    if (code !== 0) throw new Error('non-zero');
  } catch {
    throw new Error(
      'Flutter SDK not found on PATH. Install Flutter ' +
        '(https://docs.flutter.dev/get-started/install) so ctx.0 can generate the ' +
        'app/ platform scaffolding, or re-run with --no-platforms to skip it.',
    );
  }
}

/**
 * Generate the Flutter platform scaffolding (android/ios/web/etc., .metadata,
 * analysis_options.yaml, .gitignore) into `appDir` via `flutter create`. The ctx.0
 * mobile overlay is applied on top afterwards and owns lib/, test/ and pubspec.yaml.
 */
export async function scaffoldFlutterPlatforms(appDir: string, vars: TemplateVars): Promise<void> {
  await ensureFlutterAvailable();
  await fs.ensureDir(appDir);

  const { code, stderr } = await run(
    'flutter',
    ['create', '--empty', '--org', vars.org, '--project-name', vars.appSlug, appDir],
    { stdio: 'pipe' },
  );
  if (code !== 0) {
    throw new Error(`\`flutter create\` failed (exit ${code}).${stderr ? `\n${stderr.trim()}` : ''}`);
  }

  // The sample widget test references the default counter app and will not compile
  // against the ctx.0 app; ctx.0 ships its own tests under test/.
  await fs.remove(path.join(appDir, 'test', 'widget_test.dart'));
}
