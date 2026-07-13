## 0.1.0

- Initial release of the `ctx` scaffolder CLI:
  - `ctx0 create app <name>` — materializes the mobile template with `App`
    placeholder parameterization (package name, bundle ids, signing
    headers), hosted security-plane dependency, `.ctx/manifest.json`, and
    optional `--with` integrations.
  - `ctx0 status` / `enable` / `disable` / `doctor` — the marker-block
    engine and integrity checks, ported from the template's
    `tool/scaffold.dart`, driven by the repo's `.ctx/integrations.json`.
  - `ctx0 docs sync` — materializes the installed security packages'
    embedded consumer docs into `docs/packages/` with version headers.
