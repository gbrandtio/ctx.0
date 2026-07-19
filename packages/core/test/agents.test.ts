import { describe, expect, it } from 'vitest';
import {
  AGENTS_BLOCK_END,
  AGENTS_BLOCK_START,
  composeAgentsDoc,
  featureDocPath,
  renderFeatureDoc,
  type AgentsFragment,
} from '../src/agents.js';

const preamble = '# AGENTS.md — Acme\n\nStatic preamble.\n';

const frags: AgentsFragment[] = [
  { id: 'ping', summary: 'Secure echo.', body: 'Ping guidance.' },
  { id: 'auth', summary: 'Login.', body: 'Auth guidance.' },
];

describe('featureDocPath', () => {
  it('maps a feature id to an uppercase doc under docs/features', () => {
    expect(featureDocPath('auth')).toBe('docs/features/AUTH.md');
    expect(featureDocPath('payments_stripe')).toBe('docs/features/PAYMENTS_STRIPE.md');
  });
});

describe('composeAgentsDoc', () => {
  it('appends a delimited routing table after the preamble', () => {
    const out = composeAgentsDoc(preamble, frags);
    expect(out).toContain('Static preamble.');
    expect(out).toContain(AGENTS_BLOCK_START);
    expect(out).toContain(AGENTS_BLOCK_END);
    // Table routes to each feature's dedicated doc rather than inlining the body.
    expect(out).toContain('| Feature | Docs |');
    expect(out).toContain('| ping — Secure echo. | `docs/features/PING.md` |');
    expect(out).toContain('| auth — Login. | `docs/features/AUTH.md` |');
    expect(out).not.toContain('Ping guidance.');
    // Block comes after the preamble.
    expect(out.indexOf('Static preamble.')).toBeLessThan(out.indexOf(AGENTS_BLOCK_START));
  });

  it('preserves feature order', () => {
    const out = composeAgentsDoc(preamble, frags);
    expect(out.indexOf('docs/features/PING.md')).toBeLessThan(
      out.indexOf('docs/features/AUTH.md'),
    );
  });

  it('is idempotent: regenerating replaces the block in place', () => {
    const once = composeAgentsDoc(preamble, frags);
    const twice = composeAgentsDoc(once, frags);
    expect(twice).toBe(once);
    // Exactly one block, not nested/duplicated.
    expect(twice.match(new RegExp(AGENTS_BLOCK_START, 'g'))?.length).toBe(1);
    expect(twice.match(new RegExp(AGENTS_BLOCK_END, 'g'))?.length).toBe(1);
  });

  it('regenerates the block when the feature set changes (disable path)', () => {
    const full = composeAgentsDoc(preamble, frags);
    const reduced = composeAgentsDoc(full, [frags[0]!]);
    expect(reduced).toContain('docs/features/PING.md');
    expect(reduced).not.toContain('docs/features/AUTH.md');
    expect(reduced.match(new RegExp(AGENTS_BLOCK_START, 'g'))?.length).toBe(1);
  });

  it('renders a placeholder when no features are enabled', () => {
    const out = composeAgentsDoc(preamble, []);
    expect(out).toContain('No optional features are enabled');
    expect(out).toContain('Static preamble.');
  });

  it('handles a missing preamble by emitting just the block', () => {
    const out = composeAgentsDoc('', frags);
    expect(out.startsWith(AGENTS_BLOCK_START)).toBe(true);
    expect(out).toContain('docs/features/PING.md');
  });
});

describe('renderFeatureDoc', () => {
  it('wraps the fragment body in a titled, do-not-edit doc', () => {
    const doc = renderFeatureDoc(frags[1]!);
    expect(doc.startsWith('# auth — Login.')).toBe(true);
    expect(doc).toContain('do not hand-edit');
    expect(doc).toContain('Auth guidance.');
    expect(doc.endsWith('\n')).toBe(true);
  });

  it('omits the body section when the fragment has none', () => {
    const doc = renderFeatureDoc({ id: 'bare', summary: 'No body.', body: '   ' });
    expect(doc).toContain('# bare — No body.');
    expect(doc).toContain('do not hand-edit');
  });
});
