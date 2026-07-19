import path from 'node:path';
import { createRequire } from 'node:module';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';
import type { CallArgs, CallName, CallResult } from '@ctx0/engine-server/contract';
import { cliVersion } from './version.js';

/**
 * The CLI's side of the engine contract.
 *
 * The CLI holds no scaffolding logic and does not import the engine: it spawns
 * `ctx0-engine` and calls it over JSON-RPC 2.0 on stdio. Everything it knows
 * about the engine comes from the contract's types, so an engine written in
 * another language can take this one's place by answering the same calls — and
 * this CLI could equally be replaced by one written in Go against the same
 * contract.
 */
export class Engine {
  private constructor(private readonly client: Client) {}

  /** Spawn the engine and complete the handshake. */
  static async start(): Promise<Engine> {
    const client = new Client({ name: 'ctx0-cli', version: cliVersion() });
    await client.connect(
      new StdioClientTransport({ command: process.execPath, args: [enginePath()] }),
    );
    return new Engine(client);
  }

  /**
   * Make one call. A failure the engine reports (an unknown feature, a
   * non-empty target directory) is rethrown with its message intact, so the
   * CLI's top-level handler prints it like any other error.
   */
  async call<K extends CallName>(name: K, args: CallArgs<K>): Promise<CallResult<K>> {
    const response = await this.client.callTool({ name, arguments: args });

    if (response.isError) {
      throw new Error(textOf(response.content) || `Engine call "${name}" failed.`);
    }
    if (!response.structuredContent) {
      throw new Error(`Engine call "${name}" returned no result.`);
    }
    return response.structuredContent as CallResult<K>;
  }

  /** Shut the engine down. Safe to call more than once. */
  async stop(): Promise<void> {
    await this.client.close();
  }
}

/** Run `body` with a running engine, shutting it down whatever the outcome. */
export async function withEngine<T>(body: (engine: Engine) => Promise<T>): Promise<T> {
  const engine = await Engine.start();
  try {
    return await body(engine);
  } finally {
    await engine.stop();
  }
}

/** Absolute path of the engine executable, resolved through its package. */
function enginePath(): string {
  const require = createRequire(import.meta.url);
  const manifest = require.resolve('@ctx0/engine-server/package.json');
  return path.join(path.dirname(manifest), 'dist', 'index.js');
}

/** Join the text blocks of a tool response, which is where an error message arrives. */
function textOf(content: unknown): string {
  if (!Array.isArray(content)) return '';
  return content
    .filter((c): c is { type: 'text'; text: string } => (c as { type?: string }).type === 'text')
    .map((c) => c.text)
    .join('\n')
    .trim();
}
