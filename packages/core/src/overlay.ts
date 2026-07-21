import path from 'node:path';
import crypto from 'node:crypto';
import fs from 'fs-extra';
import { isProbablyBinary, substitute } from './substitute.js';
import { sortUtf8 } from './order.js';
import type { TemplateVars, WiringEdit } from './types.js';

/**
 * Root entries that are engine metadata and must never be copied into a
 * workspace: the manifest, the AGENTS.md fragment, and the `l10n/` directory of
 * translation fragments (merged per selected locale by `composeLocales`).
 */
const OVERLAY_META = new Set(['feature.json', 'agents.md', 'l10n']);

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
      const entries = sortUtf8(await fs.readdir(absSrc));
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
  return sortUtf8(written);
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
      for (const name of sortUtf8(await fs.readdir(absSrc))) {
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
 *
 * Line endings are preserved: the file is split on LF, so a CR before the LF
 * stays attached to its line and is written back untouched. When the anchor line
 * is CRLF-terminated, the inserted block is CRLF-terminated to match. Both the
 * idempotency check and the anchor search ignore CR, so a template checked out
 * with CRLF wires exactly like one checked out with LF.
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
    const needle = stripCr(insertText).trim();
    if (needle.length > 0 && stripCr(original).includes(needle)) {
      continue; // already wired
    }
    const lines = original.split('\n');
    const marker = `ctx:anchor:${edit.anchor}`;
    const idx = lines.findIndex((l) => l.includes(marker));
    if (idx === -1) {
      throw new Error(`Anchor "${edit.anchor}" not found in ${targetFile}.`);
    }
    // A CR left on the anchor line means this file uses CRLF; match it so the
    // inserted block does not introduce mixed endings.
    const crlf = lines[idx]!.endsWith('\r');
    // The trailing CR terminates the block's last line: the LF is supplied by
    // the join below, which is what turns the spliced entry back into lines.
    const block = crlf ? `${stripCr(insertText).split('\n').join('\r\n')}\r` : insertText;
    lines.splice(idx + 1, 0, block);
    await fs.writeFile(abs, lines.join('\n'), 'utf8');
  }
}

function stripCr(s: string): string {
  return s.split('\r').join('');
}

function toPosix(p: string): string {
  return p.split(path.sep).join('/');
}
