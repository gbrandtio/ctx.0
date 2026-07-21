# ADR-0004: Templates are data — a feature is a directory and a manifest

**Status**: Accepted — recorded retroactively, 2026-07-21.

## Context

A scaffolder has to decide how a "feature" is expressed. The common approach is code: a
generator function per feature that writes files, edits others, and registers itself in a
list. It is flexible, and it has two costs that compound. Every feature is a program that
must be read to know what it produces, and the generator becomes the bottleneck — adding a
feature means editing the engine.

The alternative is a template language: files full of `{{#if}}` blocks. That trades one
problem for a worse one — the templates stop being valid source. They cannot be compiled,
analysed by an IDE, or tested before composition, and every conditional is a branch nobody
exercises.

ctx.0 needs a third property the others do not give: a feature has two halves in two
ecosystems (Flutter and .NET) that must stay consistent, and a security plane whose code is
the whole point of the product. Both need to be readable and testable *as ordinary source*.

## Decision

**A feature is a directory plus a `feature.json`.** No engine code, no template language.

- Each layer is a self-contained tree copied onto the workspace, with three tokens rewritten
  in file contents and path segments: `CtxApp`, `ctxapp`, `com.ctx.app`. The tokens are
  **real, valid identifiers**, so every tree compiles and its tests run on its own, before
  composition ever happens.
- Anything a layer needs in a file another layer owns is declared as a **wiring edit** —
  a target file, an anchor comment (`ctx:anchor:<name>`), and the text to insert below it —
  applied idempotently after every layer is copied.
- Everything else a feature needs the engine to know goes in `feature.json`: `sides`,
  `requires`, `nav`, `env`, `userSteps`, `deps`.
- The catalog is **discovered** by scanning the template tree, not registered in code.

Consequently the engine has no per-feature branch anywhere, and no feature list.

## Consequences

- Adding a feature is a directory, a manifest and tests. No engine change, no rebuild — the
  engine reads the trees at runtime, so a `tsx` run picks up template edits immediately.
- Templates are inspectable by ordinary tooling: `dart analyze` and `dotnet build` work on
  them, IDEs resolve symbols, and the security overlay can be reviewed as the source it is.
- Feature dependencies are declarative, so `catalog.resolve` can answer "what will this
  selection actually install" before anything is written.
- **Cost**: expressiveness is capped at copy + wiring. A feature that needs to conditionally
  restructure a file cannot; it needs an anchor, which means editing the base tree.
- **Cost**: anchors are a coupling the type system cannot see. A renamed anchor breaks every
  feature referencing it, and the failure surfaces at compose time —
  `Anchor "<x>" not found in <file>` — not at build time.
- **Cost**: the token vocabulary is fixed at three. A fourth substitution means an engine
  change.

## References

- `packages/core/src/catalog.ts` — discovery and manifest validation.
- `packages/core/src/overlay.ts` — `copyTree`, `applyWiring`.
- `packages/core/src/substitute.ts` — `TOKENS` and the rationale for real identifiers.
- `packages/core/src/types.ts` — `FeatureManifest`, `WiringEdit`.
- `templates/mobile/features/ping/feature.json` — a two-sided feature with wiring on both.
- [architecture/templates.md](../architecture/templates.md)
