#!/usr/bin/env node
import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { CallToolRequestSchema, ListToolsRequestSchema } from '@modelcontextprotocol/sdk/types.js';
import { coreVersion } from '@ctx0/core';
import { CALL_SPECS } from './contract.js';
import { dispatch } from './tools.js';

/**
 * `ctx0-engine` — the ctx.0 composition engine behind a contract.
 *
 * The engine answers the calls declared in `contract.ts` over JSON-RPC 2.0 on
 * stdio, framed as MCP so any language's MCP client can drive it (and so an
 * agent host can too, for free). The `ctx0` CLI is one such client: it spawns
 * this binary rather than importing the engine, which is what lets either side
 * be replaced by an implementation in another language.
 *
 * Results are returned twice: as `structuredContent` — the JSON a programmatic
 * client reads — and as a JSON text block, which is what an MCP client that only
 * renders content will show.
 */
const server = new Server(
  { name: 'ctx0-engine', version: coreVersion() },
  { capabilities: { tools: {} } },
);

server.setRequestHandler(ListToolsRequestSchema, () => ({
  tools: CALL_SPECS.map((spec) => ({
    name: spec.name,
    title: spec.title,
    description: spec.description,
    inputSchema: spec.inputSchema,
  })),
}));

server.setRequestHandler(CallToolRequestSchema, async (request) => {
  const { name, arguments: args } = request.params;
  try {
    const result = (await dispatch(name, args)) as Record<string, unknown>;
    return {
      content: [{ type: 'text', text: JSON.stringify(result, null, 2) }],
      structuredContent: result,
    };
  } catch (err) {
    // Failures the caller can act on — an unknown feature, a non-empty target
    // directory — are results, not transport errors, so the message reaches the
    // CLI intact and it can print it the way it prints any other failure.
    return {
      content: [{ type: 'text', text: err instanceof Error ? err.message : String(err) }],
      isError: true,
    };
  }
});

async function main(): Promise<void> {
  await server.connect(new StdioServerTransport());
}

main().catch((err) => {
  console.error(err instanceof Error ? err.message : String(err));
  process.exitCode = 1;
});
