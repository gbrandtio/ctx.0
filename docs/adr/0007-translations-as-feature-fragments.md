# ADR-0007: Translations are per-feature fragments merged by the engine

**Status**: Accepted — recorded retroactively, 2026-07-21.

## Context

Both ecosystems expect localization in a **single file per language**: Flutter's `gen-l10n`
reads `app_<code>.arb`, and .NET's `IStringLocalizer` reads `Messages[.<code>].resx`. That
shape is incompatible with the overlay model, where each feature owns a disjoint set of
files and never edits another layer's ([ADR-0004](0004-templates-as-data-not-code.md)).

Three options were available:

1. **Ship the complete ARB/resx in the base tree**, holding every string for every feature.
   The base then knows about features that may not be enabled, and a workspace ships strings
   for screens it does not contain.
2. **Use wiring to insert keys.** Anchors in a JSON/XML document are fragile, ordering
   becomes visible, and a feature's translations end up as escaped strings inside a
   manifest.
3. **Let each feature ship its own translations and have the engine merge them.**

There is a second requirement: a workspace should carry only the languages it was created
with. Filtering five languages at runtime when the user chose two means shipping — and
translating — strings nobody will read.

## Decision

**Each layer ships a root-level `l10n/` directory of per-locale fragments, and the engine
merges them.**

- Fragments are `l10n/<code>.arb` (mobile) and `l10n/<code>.json` (api), one per offered
  language, at the layer root — alongside `feature.json`, not inside the overlay's file
  tree. `copyTree` skips the whole directory as engine metadata, so fragments never land in
  the workspace.
- A feature that ships text declares `"requires": ["l10n"]`.
- `composeLocales` merges the fragments of every enabled layer, in application order, into
  `app/lib/l10n/app_<code>.arb` and
  `api/src/Api/Resources/Localization/Messages[.<code>].resx`, and generates the support
  code each side needs: `l10n_support.dart` (delegates, supported locales, language names)
  and `SupportedCultures.g.cs`.
- **Only the selected locales are emitted.**
- **A key defined by two layers is a hard error.** Keys are namespaced by feature.
- A key missing for a selected locale is **not** an error — both runtimes fall back to the
  default. English is the template locale and must always be complete.

## Consequences

- A feature is genuinely self-contained: its strings live with it, and enabling or disabling
  it adds or removes exactly its translations.
- A workspace ships exactly the languages the user chose, in the files each toolchain
  expects.
- The support libraries are generated rather than shipped, so they name precisely the
  locales this workspace has — which is also why they are only emitted when the `l10n`
  feature's overlays are present.
- **Cost**: the merge is a second composition mechanism, distinct from copy and wiring, with
  its own metadata convention and its own failure modes.
- **Cost**: duplicate keys fail the whole generation. That is deliberate — silently letting
  application order pick a winner would be worse — but it means feature authors must
  namespace (`profileTitle`, `media.tooLarge`) as a rule rather than a habit.
- **Cost**: adding a language means touching every feature that ships text, and English
  completeness is a discipline nothing enforces automatically.
- **Cost**: the .NET resource path is load-bearing.
  `IStringLocalizer<Messages>` resolves as `<root namespace>.<ResourcesPath>.<type minus
  root namespace>`, so the generated files must land in `Resources/Localization/` with
  `Api/Localization/RootNamespace.cs` declaring the root namespace.

## References

- `packages/core/src/l10n.ts` — `LOCALES`, `resolveLocales`, `composeLocales`,
  `mergeFragments`, and the generated support libraries.
- `packages/core/src/overlay.ts` — `OVERLAY_META` skips `l10n/`.
- `templates/mobile/features/auth/l10n/`, `templates/api/features/l10n/` — a fragment set
  and the localization feature itself.
- `packages/core/test/l10n.test.ts`.
- [architecture/core.md](../architecture/core.md#localization),
  [architecture/templates.md](../architecture/templates.md#translation-fragments)
