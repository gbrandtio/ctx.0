# ADR-0001: The CLI never imports the engine

**Status**: Accepted — recorded retroactively, 2026-07-21.

## Context

ctx.0 is expected to grow more than one frontend: the `ctx0` CLI today, and an agent-facing
engine server and a web portal alongside it. The obvious structure — a CLI that imports the
scaffolder as a library — makes the CLI the privileged frontend. Logic then drifts into it
one convenience at a time: a bit of catalog filtering to render a picker, a check on which
features can be tabs, a path calculation. Each is individually reasonable, and the
collective result is that a second frontend cannot produce the same workspace without
reimplementing what leaked.

The failure is not hypothetical: deciding "which features can be navigation tabs" needs the
feature manifests, and a CLI holding the catalog will answer it locally rather than ask.

## Decision

**All scaffolding logic lives in `@ctx0/core`.** The CLI does not import it — not the
package, not a helper, not a type that implies engine internals. It reaches the engine only
through the contract, by spawning `ctx0-engine`
([ADR-0002](0002-engine-over-jsonrpc-mcp-stdio.md)).

The engine returns structured results and never prints. Presentation is the frontend's only
job.

Concretely: when the CLI needs to know something about features, layouts, languages or
workspaces, **it adds a call, not an import**. `packages/cli/src/commands/create.ts` asks
`catalog.resolve` for the nav-capable set rather than inspecting the catalog it already
holds — that is the rule in its sharpest form.

## Consequences

- A second frontend in any language is a client of the same contract, and produces
  byte-identical workspaces by construction.
- The engine is testable without a terminal: `packages/core/test/compose.test.ts` composes
  into a temp directory and asserts on the tree.
- **Cost**: capability the CLI wants is a two-step change — contract, then handler — even
  when it is one line of engine code. This is the intended friction; it is what keeps the
  boundary from eroding.
- **Cost**: the CLI cannot stream progress from inside a long composition, because the
  engine does not report. `workspace.create` is one call that returns when it is done.

## References

- `packages/cli/src/engine.ts` — the client; the only route to the engine.
- `packages/cli/src/commands/create.ts` — asks `catalog.resolve` rather than deciding.
- `packages/core/src/index.ts` — the engine's header comment states the rule.
- [architecture/cli.md](../architecture/cli.md),
  [architecture/core.md](../architecture/core.md)
