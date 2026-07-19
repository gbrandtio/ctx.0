import { spawn, type ChildProcessWithoutNullStreams } from 'node:child_process';
import path from 'node:path';
import os from 'node:os';
import { fileURLToPath } from 'node:url';
import fs from 'fs-extra';
import { afterAll, beforeAll, describe, expect, it } from 'vitest';

/**
 * Drives the built server the way a frontend in another language would: spawn
 * the binary, speak JSON-RPC 2.0 over stdio, no SDK involved. This is the test
 * that proves the boundary is language-independent — everything here is one
 * `JSON.stringify` and a newline.
 */

const here = path.dirname(fileURLToPath(import.meta.url));
const SERVER = path.resolve(here, '..', 'dist', 'index.js');

interface Response {
  id: number;
  result?: { structuredContent?: Record<string, unknown>; isError?: boolean; tools?: unknown[] };
  error?: { message: string };
}

/** A minimal newline-delimited JSON-RPC client over a child process's stdio. */
class RawClient {
  private readonly pending = new Map<number, (r: Response) => void>();
  private buffer = '';
  private nextId = 1;

  constructor(private readonly child: ChildProcessWithoutNullStreams) {
    child.stdout.setEncoding('utf8');
    child.stdout.on('data', (chunk: string) => {
      this.buffer += chunk;
      let nl: number;
      while ((nl = this.buffer.indexOf('\n')) !== -1) {
        const line = this.buffer.slice(0, nl).trim();
        this.buffer = this.buffer.slice(nl + 1);
        if (!line) continue;
        const message = JSON.parse(line) as Response;
        this.pending.get(message.id)?.(message);
        this.pending.delete(message.id);
      }
    });
  }

  request(method: string, params: unknown = {}): Promise<Response> {
    const id = this.nextId++;
    return new Promise((resolve) => {
      this.pending.set(id, resolve);
      this.child.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', id, method, params })}\n`);
    });
  }

  notify(method: string, params: unknown = {}): void {
    this.child.stdin.write(`${JSON.stringify({ jsonrpc: '2.0', method, params })}\n`);
  }

  call(name: string, args: Record<string, unknown>): Promise<Response> {
    return this.request('tools/call', { name, arguments: args });
  }
}

let child: ChildProcessWithoutNullStreams;
let client: RawClient;
let tmp: string;

beforeAll(async () => {
  tmp = await fs.mkdtemp(path.join(os.tmpdir(), 'ctx0-stdio-'));
  child = spawn(process.execPath, [SERVER], { stdio: ['pipe', 'pipe', 'pipe'] });
  client = new RawClient(child);

  const init = await client.request('initialize', {
    protocolVersion: '2025-06-18',
    capabilities: {},
    clientInfo: { name: 'ctx0-stdio-conformance', version: '0' },
  });
  expect(init.error).toBeUndefined();
  client.notify('notifications/initialized');
});

afterAll(async () => {
  child.kill();
  await fs.remove(tmp);
});

describe('MCP stdio boundary', () => {
  it('advertises every engine tool with an input schema', async () => {
    const { result } = await client.request('tools/list');
    const tools = (result?.tools ?? []) as { name: string; inputSchema: unknown }[];

    expect(tools.map((t) => t.name)).toContain('workspace.create');
    expect(tools.every((t) => typeof t.inputSchema === 'object')).toBe(true);
  });

  it('returns structured results from tools/call', async () => {
    const { result } = await client.call('vars.resolve', { name: 'My App', org: 'com.Example' });

    expect(result?.structuredContent?.vars).toEqual({
      appName: 'MyApp',
      appSlug: 'my_app',
      org: 'com.example',
      bundleId: 'com.example.my_app',
    });
  });

  it('creates a workspace over the wire', async () => {
    const targetDir = path.join(tmp, 'over-the-wire');
    const { result } = await client.call('workspace.create', {
      targetDir,
      name: 'Wire',
      org: 'com.wire',
      features: ['ping'],
    });

    expect(result?.isError).toBeFalsy();
    const manifest = result?.structuredContent?.manifest as { vars: { appSlug: string } };
    expect(manifest.vars.appSlug).toBe('wire');
    expect(await fs.pathExists(path.join(targetDir, 'app', 'lib', 'app', 'shell.dart'))).toBe(true);
  });

  it('reports an engine failure as a tool error, not a transport error', async () => {
    const { result, error } = await client.call('catalog.resolve', { features: ['no_such_feature'] });

    expect(error).toBeUndefined();
    expect(result?.isError).toBe(true);
  });
});
