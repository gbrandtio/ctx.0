# AGENTS.md — Repository Router

This is the **ctx.0 monorepo**: the `ctx0` scaffolder CLI, the compiled security packages, and the two application templates it generates from. Consumers never work here — they run `ctx0 create` and get their own repo with its own `AGENTS.md`. If you are working in a *generated* application, stop: use that repo's `AGENTS.md`, not this one.

Your **first step for any task** is to identify which area the task targets and hand off to that area's own guide. Do not read further docs until you have routed.

## Routing

| The task involves... | Go to |
|---|---|
| Flutter, Dart, widgets, Bloc/Cubit state, mobile UI/UX, localization, mobile caching (Hive), app builds/signing | [`templates/mobile/AGENTS.md`](templates/mobile/AGENTS.md) |
| Enabling/disabling scaffoldable features (Google Maps, Firebase push, Stripe checkout, profile/settings tabs, Google or email/password sign-in) | [`templates/mobile/docs/INTEGRATIONS.md`](templates/mobile/docs/INTEGRATIONS.md) and [`templates/api/docs/INTEGRATIONS.md`](templates/api/docs/INTEGRATIONS.md) — `ctx0` CLI only; in a workspace one `ctx0 enable/disable` toggles both sides |
| .NET, C#, ASP.NET Core, EF Core, PostgreSQL, migrations, RLS wiring, Stripe webhooks, FCM dispatch, PostGIS | [`templates/api/AGENTS.md`](templates/api/AGENTS.md) |
| The security plane itself (RASP, device identity, signing, ALE, JWT, RBAC catalog, envelope encryption, RLS interceptors, protocol) | `packages/ctx0_mobile_security/` and `packages/dotnet/Ctx0.Security*/` — see the **security-plane rules** below |
| The scaffolder (`ctx0 create/enable/disable/doctor/docs sync/upgrade`, marker engine, template packing) | `packages/ctx0_cli/` (+ `tool/pack_templates.dart`); the toggle catalogs live in each template's `.ctx/integrations.json` |
| Business context or feature specs (`docs/core-business/`, `docs/features/`) | Top-level `docs/`; these are the fill-in contracts shipped into generated repos. |
| Repo-level docs (this file, `README.md`), licensing, repository structure | Stay here; keep changes consistent with all areas. |

### Cross-cutting tasks (touch both templates)

Some tasks span the client/server boundary. Route to **both** guides and keep the two sides consistent:

- **API contract changes** (new/changed endpoints, DTOs): implement per `templates/api/AGENTS.md`, then refresh the exported OpenAPI spec at `templates/mobile/docs/API/swagger.json` (see `templates/mobile/docs/API/README.md`) and update the affected mobile feature docs.
- **Security protocol changes** (request signing, ALE, token lifecycle): the API-side package docs are the source of truth (`templates/api/docs/security/APPLICATION_LAYER_SECURITY.md`, `.../AUTHENTICATION.md`); the mobile docs (`templates/mobile/docs/SECURITY.md`, `.../HTTP_HANDLING.md`) and the shared spec (`docs/features/AUTHENTICATION.md`) must mirror them exactly. A change to one side without the other is a defect.
- **New end-to-end capabilities**: spec first (`docs/features/FEATURE_SPEC_TEMPLATE.md`), then follow each template's triage independently, and extend both `.ctx/integrations.json` catalogs if the capability is optional.

## Security-plane rules (packages/)

1. The plane ships **only** as the compiled packages `ctx0_mobile_security`, `Ctx0.Security`, `Ctx0.Security.Abstractions`, `Ctx0.Security.EfCore`. Never re-inline any of it into the templates; `ctx0 doctor` (and the templates' fallback `tool/scaffold.dart doctor`) fail if a vendored copy or a `dependency_overrides` appears.
2. The mobile and API planes implement **one wire protocol**. Any change to the signing string, ALE scheme, or security headers must: bump `protocol.txt` + the protocol constants in **both** packages, and deliberately regenerate `packages/protocol/wire_protocol_vectors.json` (the golden vectors both test suites assert against). `ctx0 doctor` in a workspace verifies the lock.
3. Package versions are **independent**; compatibility is expressed solely by the protocol version. Each package embeds its consumer-facing doc (README) — `ctx0 docs sync` materializes it into generated repos, so keep those READMEs accurate in the same change as the code.
4. Check `templates/api/docs/security/audits/SECURITY_HARDENING_CHECKLIST.md` before modifying anything security-adjacent; never weaken a documented control.

## Publish flow (maintainers)

`dart run tool/pack_templates.dart` embeds `templates/{mobile,api}` into `packages/ctx0_cli/templates/` (gitignored; `.pubignore` ships it) — run it before publishing `ctx0_cli`. Publishing order: security packages → CLI. The templates in this repo stay runnable reference apps wired to the packages via repo-local paths, which `ctx0 create` rewrites to hosted references.

## Repo-Wide Rules

1. **Placeholder naming**: `App` is the global rename target (`AppApi`, `AppDbContext`, `X-App-Signature`, ...) — `ctx0 create` parameterizes it. Never introduce product-specific names into template docs or code; never hardcode brand values. Branding assets belong in `templates/mobile/docs/brand-kit/`.
2. **Docs are the contract**: if your change affects architecture, security, features, or performance, updating the corresponding docs is part of the change, not a follow-up.
3. **Verification is layered**: template work must keep `flutter analyze && flutter test`, `dotnet build && dotnet test`, and `ctx0 doctor` green; scaffolder/package work must additionally survive the e2e generation check (`ctx0 create workspace <x> --local-packages` → both sides build and test, workspace doctor green).
4. **Token efficiency**: read only the docs the triage points you to; keep any docs you write factual, deduplicated, and routed from the appropriate `AGENTS.md`.
