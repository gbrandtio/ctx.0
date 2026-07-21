# Architecture decision records

An ADR records **one decision**: what was decided, the context that forced the choice, and
what it costs. It is written once and not revised — if a decision is replaced, a new ADR
supersedes it and both stay in the tree, so the reasoning behind today's structure remains
readable alongside the reasoning it replaced.

These describe *why* ctx.0 is shaped the way it is. For *what* the shape is, see
[../architecture/](../architecture/).

## Index

| # | Decision | Status |
|---|---|---|
| [0001](0001-cli-never-imports-core.md) | The CLI never imports the engine | Accepted |
| [0002](0002-engine-over-jsonrpc-mcp-stdio.md) | The engine is reached over JSON-RPC 2.0 on stdio, framed as MCP | Accepted |
| [0003](0003-versioned-contract-in-one-file.md) | The contract is one dependency-free file, and it is versioned | Accepted |
| [0004](0004-templates-as-data-not-code.md) | Templates are data — a feature is a directory and a manifest | Accepted |
| [0005](0005-vendored-security-overlay.md) | The security plane is vendored source, always on | Accepted |
| [0006](0006-deterministic-composition-ordering.md) | Every derived list is sorted in UTF-8 byte order | Accepted |
| [0007](0007-translations-as-feature-fragments.md) | Translations are per-feature fragments merged by the engine | Accepted |
| [0008](0008-reversible-workspace-manifest.md) | The workspace records every layer, its files and its source hash | Accepted |

All eight were **recorded retroactively on 2026-07-21**: the decisions were made as the
system was built and were documented afterwards. They state this in their status line so no
reader mistakes them for pre-decision proposals.

## Writing a new one

File name: `NNNN-kebab-case-title.md`, with `NNNN` the next unused number. Numbers are never
reused, including for superseded records.

Structure:

```markdown
# ADR-NNNN: <the decision, as a statement>

**Status**: Accepted, <date>.   (or: Superseded by ADR-NNNN, <date>.)

## Context
The forces that made this a real choice. What breaks under the obvious alternative.

## Decision
What was decided, stated concretely enough to check code against.

## Consequences
What this buys — and what it costs. The costs are the part worth writing.

## References
The files that embody the decision, and the architecture docs that describe it.
```

Two rules keep these useful:

- **Write the costs honestly.** An ADR listing only benefits is marketing; the reason to
  keep these is so the next person can tell whether a cost is one that was accepted or one
  that crept in.
- **One decision per file.** If the title needs an "and", it is two ADRs.

Link the new ADR from the index above, and from the invariant it explains in the relevant
[architecture](../architecture/) document.
