# ADR-0008: The workspace records every layer, its files and its source hash

**Status**: Accepted — recorded retroactively, 2026-07-21.

## Context

Once a workspace is generated it leaves ctx.0's hands: the user edits it, commits it, and
comes back weeks later. Several things then need answering from the workspace alone, with no
access to the invocation that produced it:

- Which features are enabled? (`ctx0 status`)
- Which files did a given feature contribute, if it is to be removed cleanly?
- Has a layer's source template changed since this workspace was made — or has the workspace
  drifted from what was generated?
- Which protocol version do the two sides implement, and which tool version made them?

Recording only the feature ids answers the first question and none of the others. Recording
nothing makes the workspace opaque, and any later `enable`/`disable` would have to guess
which files belong to which feature — the one operation where guessing is unacceptable.

## Decision

**`.ctx/manifest.json` records every applied layer with the files it wrote and the hash of
the source that wrote them.** Schema version 3.

| Field | Purpose |
|---|---|
| `schema` | The manifest's own version — `3` |
| `ctx0Version` | The tool that generated the workspace (the CLI passes its own version; the engine version is the fallback) |
| `protocolVersion` | Read from `protocol/protocol.json` at create time |
| `vars` | `appName`, `appSlug`, `org`, `bundleId` |
| `features` | Every layer in application order: `{ id, files, hash }` |
| `navigation` | `{ layout, tabs }` |
| `localization` | `{ default, locales }` |

- **`files`** is the complete list of workspace-relative paths that layer wrote, in
  `sortUtf8` order, POSIX-separated — so a manifest written on Windows matches one written
  on Linux, and a removal can delete exactly what was added.
- **`hash`** is a SHA-256 over the layer's **pre-substitution** source tree. It identifies
  the template independent of the app name it was rendered with, so two workspaces generated
  from the same templates carry the same hashes.
- **Layer ids** are the reserved `workspace`, `app_base`, `api_base`, `security_mobile`,
  `security_api`, and `<featureId>:<side>` for features. A consumer recovers a feature id by
  splitting on `:`; the reserved ids contain no `:` and never collide with a catalog id.
- The file's **presence is the definition of a workspace** (`isWorkspace`).

## Consequences

- `ctx0 status` works entirely from the manifest plus the catalog, inside any workspace.
- A future `enable`/`disable` has what it needs: the exact file list to remove, and the
  application order to re-derive generated artifacts. The wiring mechanism is already
  idempotent, so enable → disable → enable is a no-op.
- Drift and tampering are detectable by rehashing the template source, and per-layer, so a
  report can name the layer.
- The workspace is self-describing for support purposes: tool version, protocol version, and
  the exact selection are all in one file.
- **Cost**: the manifest grows with the workspace — it lists every generated file. That is
  the price of exact removal.
- **Cost**: it is a persisted format on users' disks. Layer ids and field names are
  effectively public API, and a shape change needs a `schema` bump and a migration path.
- **Cost**: hashes go stale by design. Editing a template makes existing workspaces report
  drift — which is information, not a defect, but it means "drift" must be presented as
  "differs from the current templates", not "corrupted".
- **Cost**: files a user adds or edits after generation are not tracked, so the manifest
  describes what ctx.0 wrote, not the current state of the tree.

## References

- `packages/core/src/types.ts` — `WorkspaceManifest`, `AppliedFeature`.
- `packages/core/src/manifest.ts` — `MANIFEST_REL`, `readManifest`, `writeManifest`,
  `isWorkspace`.
- `packages/core/src/compose.ts` — `applyLayer` records `{ id, files, hash }`.
- `packages/core/src/overlay.ts` — `hashTree` hashes pre-substitution source.
- `packages/engine-server/src/tools.ts` — `workspace.status` splits layer ids on `:`.
- [architecture/generated-workspace.md](../architecture/generated-workspace.md#ctxmanifestjson)
