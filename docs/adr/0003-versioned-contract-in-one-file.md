# ADR-0003: The contract is one dependency-free file, and it is versioned

**Status**: Accepted — recorded retroactively, 2026-07-21.

## Context

With the CLI and the engine in separate processes
([ADR-0002](0002-engine-over-jsonrpc-mcp-stdio.md)), the two sides share exactly one thing:
the set of calls, their arguments and their results. Where that shared knowledge lives
determines whether the boundary holds.

If the types live next to the handlers, importing them drags the engine's dependency graph
into the CLI, and the "no import" rule survives only by discipline. If they are duplicated
on both sides, they drift. If they exist only as runtime JSON Schemas, the CLI loses type
safety and every call site becomes stringly-typed.

There is also a lifecycle problem: a CLI and an engine can be installed and updated
separately — a linked workspace, a global install, a published pair. A version skew where
`workspace.create` gained a required argument must be *detectable*, not something the user
discovers as a confusing failure.

## Decision

**One file — `packages/engine-server/src/contract.ts` — declares the whole contract, and it
imports nothing.** Types and JSON Schemas only, no logic, no dependency on `@ctx0/core`.

It expresses the contract twice, deliberately:

- **`Calls`**, a TypeScript interface mapping each call name to its `args` and `result`.
  This makes `Engine.call('vars.resolve', {…})` typed on the client side with no code
  generation.
- **`CALL_SPECS`**, an array of `{ name, title, description, inputSchema }`. This is the
  runtime surface, published as `tools/list`, so a client that cannot import TypeScript
  discovers the same contract at runtime.

The CLI's import of this file is **type-only**, so it disappears at compile time and no
runtime edge exists.

**`CONTRACT_VERSION` is bumped whenever a call's arguments or result change shape**, and
`engine.info` reports it alongside the engine and protocol versions, so a mismatched pair
can be detected rather than failing obscurely. It is currently `'2'`.

The package exports the contract on its own subpath (`@ctx0/engine-server/contract`), so a
consumer takes the contract without taking the server.

## Consequences

- Either side can be replaced by an implementation in another language — a Go CLI over this
  engine, a Rust engine under this CLI — by honouring one readable file.
- Argument validation in `dispatch` uses the *published* schema, so a client that speaks the
  contract gets a clear message about what it got wrong.
- The contract is the design step: adding a capability starts by writing down the call.
- **Cost**: the shapes are written twice (interface and schema) and can drift from each
  other. `test/tools.test.ts` covers every call to catch this.
- **Cost**: `WorkspaceManifest` is redeclared here rather than imported from `@ctx0/core`,
  with `schema: number` instead of the literal `3` — so a client reading a manifest one
  schema version ahead still type-checks.
- **Cost**: version discipline is manual. Nothing enforces the bump but review.

## References

- `packages/engine-server/src/contract.ts` — `CONTRACT_VERSION`, `Calls`, `CALL_SPECS`.
- `packages/engine-server/src/tools.ts` — `dispatch` validates against `spec.inputSchema`.
- `packages/cli/src/engine.ts` — the type-only import and the typed `call`.
- `packages/engine-server/package.json` — the `./contract` export subpath.
- [architecture/engine-server.md](../architecture/engine-server.md)
