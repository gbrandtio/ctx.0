# ADR-0002: The engine is reached over JSON-RPC 2.0 on stdio, framed as MCP

**Status**: Accepted — recorded retroactively, 2026-07-21.

## Context

[ADR-0001](0001-cli-never-imports-core.md) puts a boundary between the frontends and the
engine. A boundary needs a transport. The candidates:

1. **In-process library call** — rejected by ADR-0001; it is not a boundary.
2. **A bespoke line protocol on stdio** — simple, but every client writes its own framing,
   and agent hosts get nothing.
3. **HTTP** — needs a port, a lifecycle, and a story for authentication and concurrency, all
   to serve a single local caller.
4. **JSON-RPC 2.0 on stdio, framed as MCP** — a standard framing with mature client and
   server SDKs in several languages.

A second requirement pushed hard on the choice: ctx.0 should be drivable by an agent. A
scaffolder is exactly the kind of tool an agent should be able to invoke — "create me a
workspace with auth and profile" — and building a separate agent integration alongside the
CLI transport would mean maintaining two surfaces over one engine.

## Decision

The engine is a **binary that speaks JSON-RPC 2.0 on stdin/stdout, framed as MCP**, with
each contract call exposed as an MCP tool.

`packages/engine-server/src/index.ts` runs an MCP `Server` over `StdioServerTransport`;
`packages/cli/src/engine.ts` is an MCP `Client` over `StdioClientTransport` that spawns the
engine with `process.execPath`. The CLI is one client among possible others, with no
privileged path.

Results are returned twice per call: as `structuredContent` (what a programmatic client
reads) and as a pretty-printed JSON text block (what a content-rendering MCP client shows).

Errors the caller can act on — an unknown feature, a non-empty target directory — are
returned as tool results with `isError: true`, **not** as JSON-RPC transport errors, so the
engine's message survives to the caller's error handler intact.

## Consequences

- Registering `ctx0-engine` as an MCP server in an agent host makes all nine calls available
  as tools, with titles and descriptions, for no extra code.
- Any language with an MCP client can drive the engine; any language can replace the engine
  under the existing CLI.
- Process isolation is free: an engine crash cannot corrupt the CLI, and the engine's cwd
  is irrelevant because paths crossing the boundary are absolute (enforced for `targetDir`).
- **Cost**: a process spawn and an MCP handshake per command run. Immaterial next to
  `flutter create`, but it is not zero.
- **Cost**: every argument must be JSON-serializable, and results cannot carry behaviour —
  no callbacks, no streams, no progress.
- **Cost**: the MCP SDK is a dependency of both the CLI and the engine server.

## Alternatives rejected

- **Bespoke stdio protocol** — saves a dependency, loses every MCP client and the agent
  story, and means hand-writing framing on both sides.
- **HTTP** — port allocation, lifecycle and auth for one local caller.

## References

- `packages/engine-server/src/index.ts` — the transport and the error convention.
- `packages/cli/src/engine.ts` — `Engine.start`, `Engine.call`, `withEngine`.
- `packages/engine-server/test/stdio.test.ts` — drives the built engine with a hand-rolled
  JSON-RPC client, proving the protocol is implementable without the SDK.
- [architecture/engine-server.md](../architecture/engine-server.md)
