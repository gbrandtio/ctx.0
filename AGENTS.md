# AGENTS.md — Repository Router

This repository contains two independent application templates. Your **first step for any task** is to identify which template the task targets and hand off to that template's own `AGENTS.md`, which is the binding guide for all work inside it. Do not read further docs until you have routed.

## Routing

| The task involves... | Go to |
|---|---|
| Flutter, Dart, widgets, Bloc/Cubit state, mobile UI/UX, localization, mobile caching (Hive), mobile security (RASP, secure storage, client-side signing/ALE), app builds/signing | [`mobile-template/AGENTS.md`](mobile-template/AGENTS.md) |
| .NET, C#, ASP.NET Core, EF Core, PostgreSQL, migrations, RLS, server-side auth (JWT/refresh tokens/RBAC), envelope encryption, middleware, Stripe webhooks, FCM dispatch, PostGIS | [`api-template/AGENTS.md`](api-template/AGENTS.md) |
| Business context or feature specs (`docs/core-business/`, `docs/features/`) | Top-level `docs/`; these are the fill-in contracts both templates consume. |
| Repo-level docs (this file, `README.md`), licensing, repository structure | Stay here; keep changes consistent with both templates. |

### Cross-cutting tasks (touch both templates)

Some tasks span the client/server boundary. Route to **both** guides and keep the two sides consistent:

- **API contract changes** (new/changed endpoints, DTOs): implement per `api-template/AGENTS.md`, then refresh the exported OpenAPI spec at `mobile-template/docs/API/swagger.json` (see `mobile-template/docs/API/README.md`) and update the affected mobile feature docs.
- **Security protocol changes** (request signing, ALE, token lifecycle): the API docs are the source of truth for the protocol (`api-template/docs/security/APPLICATION_LAYER_SECURITY.md`, `.../AUTHENTICATION.md`); the mobile docs (`mobile-template/docs/SECURITY.md`, `.../HTTP_HANDLING.md`) and the shared spec (`docs/features/AUTHENTICATION.md`) must mirror them exactly. A change to one side without the other is a defect. Update both in the same change.
- **New end-to-end features**: read the business context first (`docs/core-business/`), then the feature's spec in `docs/features/<FEATURE>.md` — if it doesn't exist, copy `docs/features/FEATURE_SPEC_TEMPLATE.md` and fill it in with the user before implementing. Then follow each template's triage independently.

## User Workflow (what this repository is for)

The templates ship complete, configurable capabilities (auth, payments, maps, push, defence in depth, performance, state management, app shell). Users insert **business logic**:

1. Fill in `docs/core-business/` (business case + client specs).
2. Spec each feature by copying `docs/features/FEATURE_SPEC_TEMPLATE.md`.
3. Implement by configuring and extending the shipped capabilities — mobile via the `FeatureModule` contract (`mobile-template/docs/APP_SHELL.md`), API via the add-a-feature recipe (`api-template/docs/architecture/EXTENDING_THE_TEMPLATE.md`). Never rebuild what the template ships.

## Repo-Wide Rules

1. **Placeholder naming**: `App` is the global rename target (`AppApi`, `AppDbContext`, `X-App-Signature`, `app_user`, ...). Never introduce product-specific names into template docs or code; never hardcode brand values.Branding assets belong in `mobile-template/docs/brand-kit/`.
2. **Docs are the contract**: if your change affects architecture, security, features, or performance, updating the corresponding docs is part of the change, not a follow-up.
3. **Never weaken a documented security control**: check `api-template/docs/security/audits/SECURITY_HARDENING_CHECKLIST.md` before modifying anything security-adjacent.
4. **Token efficiency**: read only the docs the template's triage points you to; keep any docs you write factual, deduplicated, and routed from the appropriate `AGENTS.md`.
