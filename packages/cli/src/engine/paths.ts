import { fileURLToPath } from 'node:url';
import path from 'node:path';
import fs from 'fs-extra';

/**
 * Resolve the root of the template trees.
 *
 * Two layouts are supported:
 *  - development / monorepo: templates live at the repo root `templates/`
 *    (../../.. from `packages/cli/dist/engine`).
 *  - published npm package: a build step copies `templates/` next to `dist/`
 *    (i.e. `packages/cli/templates`) so it ships inside the tarball.
 */
export function templatesRoot(): string {
  const here = path.dirname(fileURLToPath(import.meta.url)); // .../packages/cli/dist/engine
  const candidates = [
    path.resolve(here, '..', '..', 'templates'), // packages/cli/templates (published)
    path.resolve(here, '..', '..', '..', '..', 'templates'), // repo root (monorepo dev)
  ];
  for (const c of candidates) {
    if (fs.existsSync(path.join(c, 'workspace'))) return c;
  }
  throw new Error(
    `Could not locate the ctx.0 template root. Looked in:\n  ${candidates.join('\n  ')}`,
  );
}

export interface TemplateLayout {
  root: string;
  workspace: string;
  mobileBase: string;
  apiBase: string;
  securityMobile: string;
  securityApi: string;
  mobileFeatures: string;
  apiFeatures: string;
  protocol: string;
}

export function templateLayout(): TemplateLayout {
  const root = templatesRoot();
  return {
    root,
    workspace: path.join(root, 'workspace'),
    mobileBase: path.join(root, 'mobile', 'base'),
    apiBase: path.join(root, 'api', 'base'),
    securityMobile: path.join(root, 'security', 'mobile'),
    securityApi: path.join(root, 'security', 'api'),
    mobileFeatures: path.join(root, 'mobile', 'features'),
    apiFeatures: path.join(root, 'api', 'features'),
    protocol: path.resolve(root, '..', 'protocol'),
  };
}
