import { fileURLToPath } from 'node:url';
import path from 'node:path';
import fs from 'fs-extra';

/**
 * Resolve the root of the template trees.
 *
 * A frontend may pass an explicit root (e.g. a bundled-templates path a
 * published CLI/MCP/portal computes for itself); that value is used verbatim
 * when it contains a `workspace/` tree. Otherwise the root is auto-detected for
 * the two known layouts:
 *  - development / monorepo: templates live at the repo root `templates/`
 *    (../../.. from `packages/core/{dist,src}`).
 *  - published package: a build step places `templates/` next to `dist/`
 *    (i.e. `packages/core/templates`) so it ships inside the tarball.
 */
export function templatesRoot(explicitRoot?: string): string {
  const here = path.dirname(fileURLToPath(import.meta.url)); // .../packages/core/{dist,src}
  const candidates = explicitRoot
    ? [explicitRoot]
    : [
        path.resolve(here, '..', 'templates'), // packages/core/templates (published)
        path.resolve(here, '..', '..', '..', 'templates'), // repo root (monorepo dev)
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
  mobileShells: string;
  apiBase: string;
  securityMobile: string;
  securityApi: string;
  mobileFeatures: string;
  apiFeatures: string;
  protocol: string;
}

export function templateLayout(explicitRoot?: string): TemplateLayout {
  const root = templatesRoot(explicitRoot);
  return {
    root,
    workspace: path.join(root, 'workspace'),
    mobileBase: path.join(root, 'mobile', 'base'),
    mobileShells: path.join(root, 'mobile', 'shells'),
    apiBase: path.join(root, 'api', 'base'),
    securityMobile: path.join(root, 'security', 'mobile'),
    securityApi: path.join(root, 'security', 'api'),
    mobileFeatures: path.join(root, 'mobile', 'features'),
    apiFeatures: path.join(root, 'api', 'features'),
    protocol: path.resolve(root, '..', 'protocol'),
  };
}
