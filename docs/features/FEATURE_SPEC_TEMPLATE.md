# Feature Spec — <Feature Name>

> **TEMPLATE — copy to `docs/features/<FEATURE>.md` and fill in before implementing.**
> Agents read this spec before touching code. Keep it short, factual, and current.

## Purpose

<1–2 sentences: what the feature does for the user and why the business needs it. Link the relevant section of `../core-business/BUSINESS_CASE.md`.>

## Actors & Permissions

| Actor | Permission (`resource:action`) | Notes |
|---|---|---|
| <e.g., User> | <e.g., `notes:read`> | <add constants per `api-template/docs/security/AUTHORIZATION.md` §8> |

## Screens & Flows (mobile)

<Per screen: name, entry point (nav item / deep link / push), states (loading/success/failure/empty), primary actions. Reference the app shell contract in `mobile-template/docs/APP_SHELL.md` for navigation, header, and settings integration.>

## Endpoints & DTOs (api)

| Method & Path | Auth policy | Request → Response | Notes |
|---|---|---|---|
| <e.g., `GET /v1/notes`> | <policy> | <DTOs> | <pagination, output caching?> |

## Data & Security

- **Entities/tables**: <names, ownership relation (user-owned? project-scoped?)>
- **RLS**: <policy per table — follow the checklist in `api-template/docs/security/DATABASE_RLS_POLICIES.md` §4>
- **PII**: <fields needing envelope encryption / blind indexes, if any>

## Capabilities Used

<Which shipped capabilities this feature configures or consumes: payments (orders), notifications (outbox), spatial (geo-tagged entities), realtime (SSE), caching, ...>

## Caching & Invalidation

<Client: cacheable endpoints + which mutations invalidate which patterns (see `mobile-template/docs/CACHING_IMPLEMENTATION.md`). Server: output-cache policy/tags, if public reads.>

## Edge Cases & Business Rules

- <non-negotiable rules, limits, offline behavior, concurrency concerns>

## Acceptance Criteria

- [ ] <observable, testable statements — what must be true for the feature to be done>
