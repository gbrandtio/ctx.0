# Publishing Guide (Maintainers)

This document outlines the end-to-end publishing flow for the `ctx.0` ecosystem. 

Because `ctx.0` ships its security plane as locked compiled packages and uses the templates as reference implementations, the order of operations when publishing is extremely important.

## The Publishing Order
1. The Security Packages (`Ctx0.Security*` and `ctx0_mobile_security`)
2. The Templates (Packed into the CLI)
3. The CLI Scaffolder (`ctx0_cli`)

---

### Step 1: (Optional) Wire Protocol Updates
If your changes affect the mobile↔API wire protocol (e.g., signing string generation, ALE encryption scheme, or security headers):
1. **Bump the protocol version** in the constants of *both* security packages.
2. Manually regenerate the golden vectors in `packages/protocol/wire_protocol_vectors.json`. Both the .NET and Flutter test suites assert against this file to ensure the client and server can communicate securely.

### Step 2: Publish the API Security Packages (.NET)
The API security plane consists of three packages located in `packages/dotnet/`.

1. `Ctx0.Security.Abstractions`
2. `Ctx0.Security.EfCore`
3. `Ctx0.Security`

- Bump the versions in their `.csproj` files.
- Pack and push them to NuGet:
  ```bash
  cd packages/dotnet
  dotnet pack -c Release
  dotnet nuget push **/*.nupkg -s https://api.nuget.org/v3/index.json -k <YOUR_API_KEY>
  ```

### Step 3: Publish the Mobile Security Package (Flutter)
The mobile security plane is located in `packages/ctx0_mobile_security/`.

- Bump the version in `pubspec.yaml`.
- Ensure tests pass and the Dart analyzer is happy.
- Publish to pub.dev:
  ```bash
  cd packages/ctx0_mobile_security
  dart pub publish
  ```

### Step 4: Pack the Templates
The `ctx0` CLI does not fetch templates from the internet at runtime; it embeds them. Before publishing the CLI, you **must** pack the current state of `templates/mobile/` and `templates/api/` into the CLI package.

Run the packing tool from the root of the repository:
```bash
dart run tool/pack_templates.dart
```
This script will copy the templates into `packages/ctx0_cli/templates/` (this directory is `.gitignore`d but included in `.pubignore` so it ships with the CLI). The templates inside the repo remain runnable reference apps wired to local package paths; the scaffolder automatically rewrites these to hosted references during `ctx0 create`.

### Step 5: Publish the CLI Scaffolder
Finally, publish the `ctx0_cli` package.

- Bump the version in `packages/ctx0_cli/pubspec.yaml`.
- Ensure the templates were successfully packed in the previous step.
- Publish to pub.dev:
  ```bash
  cd packages/ctx0_cli
  dart pub publish
  ```

## Golden Rules
- **Documentation is the contract:** If you updated how a package behaves, ensure its `README.md` is updated in the exact same commit. `ctx0 docs sync` materializes these READMEs into generated repos.
- **Never publish the CLI without packing templates:** If you skip Step 4, the CLI will be published with outdated templates.
- **Maintainer Scope:** Only consumers run `ctx0 create`. Maintainers work in the `templates/` folders, which are runnable reference apps out of the box. Ensure `flutter test` and `dotnet test` stay green in the templates before starting the release flow.
