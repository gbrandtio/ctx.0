# ADR-0006: Every derived list is sorted in UTF-8 byte order

**Status**: Accepted — recorded retroactively, 2026-07-21.

## Context

Composition derives lists from the filesystem: directory entries during a copy, the file
list recorded per layer in the manifest, the walk order feeding an overlay hash, the keys of
a generated resource file. Order matters in each case — it determines the manifest content
and the hash value, which are how a workspace is verified later.

Filesystem readdir order is not defined, so the list must be sorted. The question is *with
which comparator*, and it is not academic:

- `Array.prototype.sort()` with no comparator compares **UTF-16 code units**. For non-BMP
  characters (emoji, some scripts) that orders differently from a byte-wise comparison of
  the UTF-8 encoding.
- `localeCompare` depends on the host locale, so the same tree composes differently on two
  machines.
- Go and Rust compare strings byte-wise natively, as does `LC_ALL=C sort`.

Since [ADR-0003](0003-versioned-contract-in-one-file.md) explicitly anticipates an engine
reimplemented in another language, a comparator that JavaScript happens to reach for by
default but Go does not would mean two conforming engines producing different hashes for the
same template tree.

## Decision

**One comparator, used everywhere:** `compareUtf8` in `packages/core/src/order.ts`, a
`Buffer.compare` of the UTF-8 encodings, exposed as `sortUtf8(values)`.

Every list the engine derives from the filesystem is sorted with it — **never a bare
`.sort()`**:

- directory entries in `copyTree`'s walk and `hashTree`'s walk;
- the file list `copyTree` returns and the manifest records;
- feature ids during catalog discovery;
- resx keys in the generated .NET resource files.

Locale selection is normalised the same way for the same reason: `resolveLocales` returns
the selection in `LOCALES` catalog order, so requesting `el,en` and `en,el` compose
identically.

## Consequences

- A given template tree composes to the same workspace on any host, and an overlay hash
  computed by this engine matches one computed by a reimplementation in Go or Rust.
- `hashTree` is meaningful as an integrity check: it identifies the template that produced a
  layer, independent of the app name it was rendered with (it hashes pre-substitution
  bytes) and of the machine it ran on.
- Manifest diffs between two generations of the same selection are empty rather than noisy.
- **Cost**: it must be applied by hand at every site. Nothing in the type system prevents a
  bare `.sort()`, so this is a review-time invariant.
- **Cost**: the ordering is byte-wise, not human-friendly. `Z` sorts before `a`, and
  accented characters sort after ASCII. That is correct here — these lists are machine
  inputs, not presentation — but a list rendered directly to a user would want different
  treatment.

## References

- `packages/core/src/order.ts` — `compareUtf8`, `sortUtf8`, and the rationale in its header.
- `packages/core/src/overlay.ts` — walk order and the returned file list.
- `packages/core/src/catalog.ts` — feature discovery order.
- `packages/core/src/l10n.ts` — `resolveLocales` catalog order, resx key order.
- [architecture/core.md](../architecture/core.md#determinism)
