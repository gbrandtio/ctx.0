The `l10n` feature (api side) makes the API answer in the caller's language.

- `Api/Localization/LocalizationBootstrap.cs` registers `AddLocalization` over
  `Resources/` and `UseRequestLocalization`, so the culture comes from the request's
  `Accept-Language` header. `UseCtxLocalization()` runs before anything that
  produces user-facing text; keep it there if you reorder the pipeline.
- `Api/Localization/SupportedCultures.g.cs` is **generated** from the languages
  chosen at create time. Satellite resource assemblies cannot be enumerated
  reliably at startup, so the list is written out as code — regenerate the
  workspace to change it rather than editing the file.
- `Resources/Localization/Messages.resx` (neutral, English) and
  `Messages.<culture>.resx` are generated too, merged from each feature's
  `l10n/<code>.json` fragments. That path is fixed: resource lookup keys off the
  root namespace declared in `Api/Localization/RootNamespace.cs`, which the
  assembly needs because it is named `Api` but rooted at `<AppName>.Api`. An
  unsupported or absent `Accept-Language` falls back to the neutral file, so every
  request gets a real message.
- Endpoints take `IStringLocalizer<Messages>` and read keys namespaced by feature
  (`auth.invalidCredentials`, `media.tooLarge`). Parameterised messages use
  positional `{0}` placeholders: `loc["media.tooLarge", options.MaxBytes]`.
- A missing key returns the key itself rather than throwing — a missing translation
  degrades to an ugly response, never to a failed request.
- Keep validation and error text out of the domain layer: strings are resolved at
  the edge, where the request's culture is known.
