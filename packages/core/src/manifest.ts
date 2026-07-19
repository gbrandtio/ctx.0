import path from 'node:path';
import fs from 'fs-extra';
import type { WorkspaceManifest } from './types.js';

export const MANIFEST_REL = path.join('.ctx', 'manifest.json');

export async function writeManifest(
  workspaceRoot: string,
  manifest: WorkspaceManifest,
): Promise<void> {
  const abs = path.join(workspaceRoot, MANIFEST_REL);
  await fs.ensureDir(path.dirname(abs));
  await fs.writeJson(abs, manifest, { spaces: 2 });
}

export async function readManifest(workspaceRoot: string): Promise<WorkspaceManifest> {
  const abs = path.join(workspaceRoot, MANIFEST_REL);
  if (!(await fs.pathExists(abs))) {
    throw new Error(
      `Not a ctx.0 workspace: ${MANIFEST_REL} not found in ${workspaceRoot}.\n` +
        `Run this command from the root of a workspace created with \`ctx0 create\`.`,
    );
  }
  return (await fs.readJson(abs)) as WorkspaceManifest;
}

export async function isWorkspace(dir: string): Promise<boolean> {
  return fs.pathExists(path.join(dir, MANIFEST_REL));
}
