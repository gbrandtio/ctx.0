import { describe, expect, it } from 'vitest';
import { pascalCase, resolveVars, slugify, substitute } from '../src/engine/substitute.js';

describe('slugify / pascalCase', () => {
  it('normalizes names to a snake slug', () => {
    expect(slugify('Acme Corp')).toBe('acme_corp');
    expect(slugify('  My-App!! ')).toBe('my_app');
    expect(slugify('foo__bar')).toBe('foo_bar');
  });

  it('pascal-cases a slug', () => {
    expect(pascalCase('acme_corp')).toBe('AcmeCorp');
    expect(pascalCase('acme')).toBe('Acme');
  });
});

describe('resolveVars', () => {
  it('derives all vars from a name and org', () => {
    const v = resolveVars('Acme App', 'com.acme');
    expect(v.appSlug).toBe('acme_app');
    expect(v.appName).toBe('AcmeApp');
    expect(v.org).toBe('com.acme');
    expect(v.bundleId).toBe('com.acme.app');
  });

  it('defaults org from the slug when omitted', () => {
    const v = resolveVars('Acme');
    expect(v.org).toBe('com.acme');
    expect(v.bundleId).toBe('com.acme.app');
  });

  it('rejects an empty name', () => {
    expect(() => resolveVars('!!!')).toThrow();
  });
});

describe('substitute', () => {
  const vars = resolveVars('Acme', 'com.acme');

  it('replaces all three tokens', () => {
    expect(substitute('namespace CtxApp.Api;', vars)).toBe('namespace Acme.Api;');
    expect(substitute('name: ctxapp', vars)).toBe('name: acme');
    expect(substitute('id "com.ctx.app"', vars)).toBe('id "com.acme.app"');
  });

  it('is case-sensitive: ctxapp does not match CtxApp', () => {
    // CtxApp -> Acme (pascal), the substring "app" inside must not be slug-replaced.
    expect(substitute('CtxApp', vars)).toBe('Acme');
  });

  it('handles the bundle token before bare tokens', () => {
    expect(substitute('com.ctx.app / CtxApp / ctxapp', vars)).toBe('com.acme.app / Acme / acme');
  });
});
