import path from 'node:path';
import fs from 'fs-extra';
import { templateLayout } from './paths.js';
import { substitute } from './substitute.js';
import type { CatalogEntry } from './catalog.js';
import type { FeatureNav, LayoutId, TemplateVars } from './types.js';

/** Workspace-relative path of the generated mobile shell. */
export const SHELL_REL = path.posix.join('app', 'lib', 'app', 'shell.dart');

/** A selectable main-navigation layout, surfaced to the CLI's layout picker. */
export interface LayoutDescriptor {
  id: LayoutId;
  /** Human-readable title shown in the picker. */
  label: string;
  /** One-line description of the structure. */
  description: string;
}

/** The layout structures offered at create time, in display order. */
export const LAYOUTS: LayoutDescriptor[] = [
  {
    id: 'bottom_nav',
    label: 'Bottom navigation bar',
    description: 'Persistent bottom tab bar (Material NavigationBar) — the classic mobile pattern.',
  },
  {
    id: 'nav_rail',
    label: 'Navigation rail',
    description: 'Side rail for tablet / desktop / wide layouts (Material NavigationRail).',
  },
  {
    id: 'drawer',
    label: 'Navigation drawer',
    description: 'Hamburger drawer menu (Material NavigationDrawer) for many destinations.',
  },
  {
    id: 'home_list',
    label: 'Simple home list',
    description: 'A single landing screen listing each feature as a tile — no persistent nav.',
  },
];

const LAYOUT_IDS = new Set<LayoutId>(LAYOUTS.map((l) => l.id));

/** Whether `id` is a known layout. */
export function isLayoutId(id: string): id is LayoutId {
  return LAYOUT_IDS.has(id as LayoutId);
}

/**
 * From an ordered list of enabled feature ids, return those that are nav-capable
 * (declare a `nav` block), preserving order. This is the candidate set for the
 * "which features are main-navigation tabs" step.
 */
export function navCapable(catalog: Map<string, CatalogEntry>, enabled: string[]): string[] {
  return enabled.filter((id) => catalog.get(id)?.manifest.nav);
}

/**
 * Generate `app/lib/app/shell.dart` for the chosen layout and tab set.
 *
 * When `tabs` is empty, a minimal placeholder shell is emitted regardless of the
 * layout. Otherwise the layout's on-disk template is loaded and its `ctx:gen:*`
 * markers are replaced with Dart derived from each tab's `nav` metadata. Every
 * tab id must be present in the catalog and declare a `nav` block.
 */
export async function composeShell(
  workspaceRoot: string,
  layout: LayoutId,
  tabs: string[],
  catalog: Map<string, CatalogEntry>,
  vars: TemplateVars,
  explicitRoot?: string,
): Promise<void> {
  if (!isLayoutId(layout)) {
    throw new Error(`Unknown layout "${layout}". Known: ${[...LAYOUT_IDS].join(', ')}.`);
  }

  const navs = tabs.map((id) => {
    const entry = catalog.get(id);
    if (!entry) {
      throw new Error(`Nav tab "${id}" is not a known feature.`);
    }
    if (!entry.manifest.nav) {
      throw new Error(`Nav tab "${id}" is not nav-capable (its feature.json declares no "nav").`);
    }
    return entry.manifest.nav;
  });

  const content = navs.length === 0 ? placeholderShell() : renderShell(layout, navs, explicitRoot);
  const abs = path.join(workspaceRoot, SHELL_REL);
  await fs.ensureDir(path.dirname(abs));
  await fs.writeFile(abs, substitute(content, vars), 'utf8');
}

/** A minimal shell for a workspace with no main-navigation tabs. */
function placeholderShell(): string {
  return `import 'package:flutter/material.dart';

/// Main navigation shell for CtxApp. No main-navigation tabs were selected at
/// create time, so this is a minimal placeholder — build your first screen here.
class CtxShell extends StatelessWidget {
  const CtxShell({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('CtxApp')),
      body: const Center(child: Text('CtxApp')),
    );
  }
}
`;
}

/** Load the layout template and fill its generation markers from the tab navs. */
function renderShell(layout: LayoutId, navs: FeatureNav[], explicitRoot?: string): string {
  const layoutDir = path.join(templateLayout(explicitRoot).mobileShells, layout);
  const templatePath = path.join(layoutDir, 'shell.dart');
  if (!fs.existsSync(templatePath)) {
    throw new Error(`Missing shell template for layout "${layout}": ${templatePath}`);
  }
  let src = fs.readFileSync(templatePath, 'utf8');

  src = replaceMarker(src, 'ctx:gen:imports', imports(navs));
  if (layout === 'home_list') {
    src = replaceMarker(src, 'ctx:gen:tiles', tiles(navs));
  } else {
    src = replaceMarker(src, 'ctx:gen:pages', pages(navs));
    src = replaceMarker(src, 'ctx:gen:destinations', destinations(layout, navs));
  }
  return src;
}

function imports(navs: FeatureNav[]): string {
  return navs.map((n) => `import '${n.import}';`).join('\n');
}

function pages(navs: FeatureNav[]): string {
  return navs.map((n) => `    ${n.page}(),`).join('\n');
}

function destinations(layout: LayoutId, navs: FeatureNav[]): string {
  // The destination lists are declared `const`, so inner widgets are implicitly
  // const — no `const` keyword is repeated on each entry.
  return navs
    .map((n) => {
      const icon = `Icon(Icons.${n.icon})`;
      const label = dartString(n.label);
      switch (layout) {
        case 'bottom_nav':
          return `    NavigationDestination(icon: ${icon}, label: ${label}),`;
        case 'nav_rail':
          return `    NavigationRailDestination(icon: ${icon}, label: Text(${label})),`;
        case 'drawer':
          return `    NavigationDrawerDestination(icon: ${icon}, label: Text(${label})),`;
        default:
          return '';
      }
    })
    .join('\n');
}

function tiles(navs: FeatureNav[]): string {
  return navs
    .map(
      (n) =>
        `      ListTile(title: Text(${dartString(n.label)}), leading: const Icon(Icons.${n.icon}), ` +
        `onTap: () => Navigator.of(context).push(MaterialPageRoute(builder: (_) => ${n.page}()))),`,
    )
    .join('\n');
}

/** Render a Dart single-quoted string literal, escaping backslashes and quotes. */
function dartString(value: string): string {
  return `'${value.replace(/\\/g, '\\\\').replace(/'/g, "\\'")}'`;
}

/** Replace the single line carrying `marker` with `block` (dropped if empty). */
function replaceMarker(src: string, marker: string, block: string): string {
  const lines = src.split('\n');
  const idx = lines.findIndex((l) => l.includes(marker));
  if (idx === -1) {
    throw new Error(`Shell template is missing the "${marker}" marker.`);
  }
  if (block === '') {
    lines.splice(idx, 1);
  } else {
    lines.splice(idx, 1, block);
  }
  return lines.join('\n');
}
