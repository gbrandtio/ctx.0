import path from 'node:path';
import fs from 'fs-extra';
import { templateLayout } from './paths.js';
import type { FeatureManifest, Side } from './types.js';

/** A feature discovered in the template tree, with the dirs holding its overlay. */
export interface CatalogEntry {
  manifest: FeatureManifest;
  /** Overlay source dir per side (only sides the feature declares). */
  dirs: Partial<Record<Side, string>>;
}

/**
 * Load the full feature catalog by scanning `templates/{mobile,api}/features/*`.
 * A feature may appear under one or both sides; its `feature.json` (identical on
 * both sides, or present on either) is the source of truth. The two always-on
 * "features" (base, security) are not part of the toggleable catalog.
 */
export function loadCatalog(): Map<string, CatalogEntry> {
  const layout = templateLayout();
  const entries = new Map<string, CatalogEntry>();

  const scan = (featuresRoot: string, side: Side) => {
    if (!fs.existsSync(featuresRoot)) return;
    for (const id of fs.readdirSync(featuresRoot)) {
      const dir = path.join(featuresRoot, id);
      if (!fs.statSync(dir).isDirectory()) continue;
      const manifestPath = path.join(dir, 'feature.json');
      if (!fs.existsSync(manifestPath)) {
        throw new Error(`Feature "${id}" (${side}) is missing feature.json at ${manifestPath}`);
      }
      const manifest = fs.readJsonSync(manifestPath) as FeatureManifest;
      validateManifest(manifest, id, side);

      const existing = entries.get(id);
      if (existing) {
        existing.dirs[side] = dir;
      } else {
        entries.set(id, { manifest, dirs: { [side]: dir } });
      }
    }
  };

  scan(layout.mobileFeatures, 'mobile');
  scan(layout.apiFeatures, 'api');
  return entries;
}

/**
 * Resolve a requested feature set into an ordered, dependency-complete list.
 * Throws on unknown ids or unsatisfiable/circular requirements.
 */
export function resolveFeatureOrder(
  requested: string[],
  catalog: Map<string, CatalogEntry>,
): string[] {
  const ordered: string[] = [];
  const visiting = new Set<string>();
  const done = new Set<string>();

  const visit = (id: string, chain: string[]) => {
    if (done.has(id)) return;
    if (visiting.has(id)) {
      throw new Error(`Circular feature dependency: ${[...chain, id].join(' -> ')}`);
    }
    const entry = catalog.get(id);
    if (!entry) {
      throw new Error(`Unknown feature "${id}". Run \`ctx0 status\` to list available features.`);
    }
    visiting.add(id);
    for (const dep of entry.manifest.requires ?? []) {
      visit(dep, [...chain, id]);
    }
    visiting.delete(id);
    done.add(id);
    ordered.push(id);
  };

  for (const id of requested) visit(id, []);
  return ordered;
}

function validateManifest(m: FeatureManifest, dirId: string, side: Side): void {
  if (m.id !== dirId) {
    throw new Error(`Feature dir "${dirId}" declares mismatched id "${m.id}".`);
  }
  if (!m.summary) throw new Error(`Feature "${dirId}" is missing a summary.`);
  if (!Array.isArray(m.sides) || m.sides.length === 0) {
    throw new Error(`Feature "${dirId}" must declare at least one side.`);
  }
  if (!m.sides.includes(side)) {
    throw new Error(
      `Feature "${dirId}" has an overlay under "${side}" but does not list "${side}" in sides.`,
    );
  }
}
