import { describe, expect, it } from 'vitest';
import {
  AGENTS_BLOCK_END,
  AGENTS_BLOCK_START,
  composeAgentsDoc,
  type AgentsFragment,
} from '../src/agents.js';

const preamble = '# AGENTS.md — Acme\n\nStatic preamble.\n';

const frags: AgentsFragment[] = [
  { id: 'ping', summary: 'Secure echo.', body: 'Ping guidance.' },
  { id: 'auth', summary: 'Login.', body: 'Auth guidance.' },
];

describe('composeAgentsDoc', () => {
  it('appends a delimited block after the preamble', () => {
    const out = composeAgentsDoc(preamble, frags);
    expect(out).toContain('Static preamble.');
    expect(out).toContain(AGENTS_BLOCK_START);
    expect(out).toContain(AGENTS_BLOCK_END);
    expect(out).toContain('### ping — Secure echo.');
    expect(out).toContain('Ping guidance.');
    expect(out).toContain('### auth — Login.');
    // Block comes after the preamble.
    expect(out.indexOf('Static preamble.')).toBeLessThan(out.indexOf(AGENTS_BLOCK_START));
  });

  it('preserves feature order', () => {
    const out = composeAgentsDoc(preamble, frags);
    expect(out.indexOf('### ping')).toBeLessThan(out.indexOf('### auth'));
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
    expect(reduced).toContain('### ping');
    expect(reduced).not.toContain('### auth');
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
    expect(out).toContain('### ping');
  });
});
