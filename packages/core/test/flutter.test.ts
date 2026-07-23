import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import os from 'node:os';
import path from 'node:path';
import { execFile, execFileSync } from 'node:child_process';
import { promisify } from 'node:util';
import fs from 'fs-extra';
import { createWorkspace } from '../src/compose.js';
import { resolveVars } from '../src/substitute.js';
import { loadCatalog } from '../src/catalog.js';
import { templatesRoot } from '../src/paths.js';

const execFileAsync = promisify(execFile);

/**
 * Mobile counterpart of build.test.ts. The generator's other tests never run the
 * Dart toolchain, so a template that fails to analyze, or that is left
 * unformatted, passes all of them. These tests close that gap:
 *
 *  - `dart format` guards every `.dart` file under templates/, which is the
 *    source a human edits directly.
 *  - `flutter analyze --fatal-infos` guards the composed app end to end, which
 *    also covers the files produced at compose time (the localization sources
 *    generated from the `.arb` catalogs, and the substituted copies of the
 *    template files).
 *
 * Both skip when the Dart or Flutter SDK is absent, so local runs without it
 * still pass. CI must provide the SDK for the guards to run.
 */
function hasCommand(command: string): boolean {
  try {
    execFileSync(command, ['--version'], { stdio: 'ignore' });
    return true;
  } catch {
    return false;
  }
}

const dartAvailable = hasCommand('dart');
const flutterAvailable = hasCommand('flutter');

// The mobile app's SDK floor (templates/mobile/base/pubspec.yaml: sdk ">=3.12.0")
// fixes the formatter style, so pin the check to it rather than the host's latest.
const LANGUAGE_VERSION = '3.12';

let tmp: string;

beforeEach(async () => {
  tmp = await fs.mkdtemp(path.join(os.tmpdir(), 'ctx0-flutter-'));
});

afterEach(async () => {
  await fs.remove(tmp);
});

describe.skipIf(!dartAvailable)('template Dart formatting', () => {
  it('every authored template .dart file is dart-format clean', async () => {
    try {
      await execFileAsync(
        'dart',
        [
          'format',
          '--output=none',
          '--set-exit-if-changed',
          `--language-version=${LANGUAGE_VERSION}`,
          templatesRoot(),
        ],
        { maxBuffer: 64 * 1024 * 1024 },
      );
    } catch (error) {
      const err = error as { stdout?: string; stderr?: string };
      throw new Error(
        `dart format reported unformatted template files (run \`dart format ` +
          `--language-version=${LANGUAGE_VERSION} templates\`):\n${err.stdout ?? ''}\n${err.stderr ?? ''}`,
      );
    }
  });
});

describe.skipIf(!flutterAvailable)('generated app analyzes', () => {
  it(
    'flutter analyze --fatal-infos is clean for a workspace with every mobile feature',
    async () => {
      const vars = resolveVars('Acme', 'com.acme');
      const targetDir = path.join(tmp, vars.appSlug);

      const catalog = loadCatalog();
      const mobileFeatures = [...catalog.values()]
        .filter((entry) => entry.manifest.sides.includes('mobile'))
        .map((entry) => entry.manifest.id)
        .sort();

      await createWorkspace({ targetDir, vars, features: mobileFeatures });

      const appDir = path.join(targetDir, 'app');
      await execFileAsync('flutter', ['pub', 'get'], {
        cwd: appDir,
        maxBuffer: 64 * 1024 * 1024,
      });

      let output: string;
      try {
        const result = await execFileAsync('flutter', ['analyze', '--fatal-infos'], {
          cwd: appDir,
          maxBuffer: 64 * 1024 * 1024,
        });
        output = `${result.stdout}\n${result.stderr}`;
      } catch (error) {
        const err = error as { stdout?: string; stderr?: string };
        throw new Error(`flutter analyze failed:\n${err.stdout ?? ''}\n${err.stderr ?? ''}`);
      }

      expect(output).toContain('No issues found!');
    },
    600_000,
  );
});
