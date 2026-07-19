import path from 'node:path';
import crypto from 'node:crypto';
import fs from 'fs-extra';
import { isProbablyBinary, substitute } from './substitute.js';
import type { TemplateVars, WiringEdit } from './types.js';

/** Files that are engine metadata and must never be copied into a workspace. */
const OVERLAY_META = new Set(['feature.json']);

/**
 * Copy an overlay/base source tree into the workspace under `destPrefix`,
 * applying token substitution to both file contents and path segments.
 *
 * Returns the list of workspace-relative paths written, in deterministic order,
 * so a later `disable` can remove exactly what was added.
 */
export async function copyTree(
  srcDir: string,
  workspaceRoot: string,
  destPrefix: string,
  vars: TemplateVars,
): Promise<string[]> {
  const written: string[] = [];
  const walk = async (rel: string) => {
    const absSrc = path.join(srcDir, rel);
    const stat = await fs.stat(absSrc);
    if (stat.isDirectory()) {
      const entries = (await fs.readdir(absSrc)).sort();
      for (const name of entries) {
        if (rel === '' && OVERLAY_META.has(name)) continue;
        await walk(rel === '' ? name : path.join(rel, name));
      }
      return;
    }
    // File: substitute path segments and content.
    const relSubbed = substitute(rel, vars);
    const wsRel = path.posix.join(destPrefix, toPosix(relSubbed));
    const absDest = path.join(workspaceRoot, wsRel);
    await fs.ensureDir(path.dirname(absDest));

    const buf = await fs.readFile(absSrc);
    if (isProbablyBinary(buf)) {
      await fs.writeFile(absDest, buf);
    } else {
      await fs.writeFile(absDest, substitute(buf.toString('utf8'), vars), 'utf8');
    }
    written.push(wsRel);
  };
  await walk('');
  return written.sort();
}

/**
 * Stable content hash of an overlay source dir (pre-substitution), used to
 * detect drift / tampering of an applied overlay later via `doctor`.
 */
export async function hashTree(srcDir: string): Promise<string> {
  const hash = crypto.createHash('sha256');
  const walk = async (rel: string) => {
    const absSrc = path.join(srcDir, rel);
    const stat = await fs.stat(absSrc);
    if (stat.isDirectory()) {
      for (const name of (await fs.readdir(absSrc)).sort()) {
        await walk(rel === '' ? name : path.join(rel, name));
      }
      return;
    }
    hash.update(toPosix(rel));
    hash.update('\0');
    hash.update(await fs.readFile(absSrc));
    hash.update('\0');
  };
  await walk('');
  return hash.digest('hex');
}

/**
 * Apply idempotent wiring edits. Each edit inserts `insert` immediately below
 * the first line containing `ctx:anchor:<anchor>`. If an identical insertion is
 * already present (idempotency check on the substituted text), it is skipped, so
 * enable -> disable -> enable is a no-op.
 */
export async function applyWiring(
  workspaceRoot: string,
  edits: WiringEdit[],
  vars: TemplateVars,
): Promise<void> {
  for (const edit of edits) {
    const targetFile = substitute(edit.file, vars);
    const abs = path.join(workspaceRoot, targetFile);
    if (!(await fs.pathExists(abs))) {
      throw new Error(`Wiring target not found: ${targetFile} (anchor "${edit.anchor}")`);
    }
    const insertText = substitute(edit.insert, vars);
    const original = await fs.readFile(abs, 'utf8');
    if (original.includes(insertText.trim()) && insertText.trim().length > 0) {
      continue; // already wired
    }
    const lines = original.split('\n');
    const marker = `ctx:anchor:${edit.anchor}`;
    const idx = lines.findIndex((l) => l.includes(marker));
    if (idx === -1) {
      throw new Error(`Anchor "${edit.anchor}" not found in ${targetFile}.`);
    }
    lines.splice(idx + 1, 0, insertText);
    await fs.writeFile(abs, lines.join('\n'), 'utf8');
  }
}

function toPosix(p: string): string {
  return p.split(path.sep).join('/');
}
