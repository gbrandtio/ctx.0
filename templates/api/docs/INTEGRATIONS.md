# Scaffoldable Features (API) — the Wiring Contract

The API's optional integrations mirror the mobile template's and are
toggled by the same tool over the same mechanism: `ctx:<id>:begin/end`
marker blocks described in `.ctx/integrations.json`. This document is
binding for any agent working in this template (AGENTS.md routes here).

## 1. What is NOT scaffoldable (read first)

The security plane — **JWT issuance, ALE, ECDSA request signing, RBAC,
envelope encryption, RLS** — ships as the compiled `Ctx0.Security`,
`Ctx0.Security.Abstractions`, and `Ctx0.Security.EfCore` packages, wired
through the single seams `AddAppSecurity`/`UseAppSecurity` (which
delegate to `AddCtxSecurity`/`UseCtxSecurity`). It has no scaffolder
entry and there is **no supported procedure to remove, weaken, fork, or
re-implement it**. `ctx0 doctor` fails if the packages are missing, the
seams are unwired, or the EF interceptors are off the DbContext.

Email/password authentication, users, orders, projects, and the in-app
notification feed are core — always on.

## 2. The one rule

> **You MUST NOT hand-edit any `ctx:` marker block, csproj integration
> condition, or `.ctx/integrations.json`. The only permitted actions
> are `ctx0 status | enable <id> | disable <id> | doctor`.** If the
> scaffolder fails, report the failure verbatim and stop.

After any enable/disable, run and pass: `ctx0 doctor && dotnet build &&
dotnet test`.

## 3. The catalog

| id | Feature | Ships | Mobile counterpart |
|---|---|---|---|
| `payments_stripe` | Stripe payment intents + webhook + SSE payment events | enabled | `payments_stripe` |
| `push_firebase` | FCM dispatch worker + firebase token endpoints | enabled | `push_firebase` |
| `auth_google` | Google Sign-In endpoint + identity link | enabled | `auth_google` |
| `maps_google` | Nearby geo-items endpoint (PostGIS) + output cache | enabled | `maps_google` |

In a `ctx0 create workspace` layout, `ctx0 enable/disable <id>` at the
workspace root toggles **both** sides; toggling only one side is an
incomplete change — say so explicitly in your summary.

## 4. How disabling works (so you don't fight it)

- Endpoint registrations, DI lines, and workers are commented out via
  marker blocks (`Program.cs`, `Extensions/ServiceCollectionExtensions.cs`,
  `Endpoints/v1/UsersEndpoints.cs`).
- Each affected csproj holds a marker-toggled `Ctx<Feature>` property;
  while disabled the vendor NuGet drops out and the feature's
  vendor-coupled sources/tests are excluded via
  `<Compile Remove ... Condition>` (e.g. `StripePaymentGateway.cs`,
  `Features/Payments/**`, `PostgresNotificationListener.cs`,
  `SpatialTests.cs`).
- **Entities, DbSets, entity configurations, and migrations always stay
  compiled** — a disabled feature's tables simply go unused. Never
  remove them: the EF model snapshot and migrations are interdependent.
- Handlers with no vendor dependency stay compiled but unreachable; the
  DI container is validated at startup, so anything requiring an
  unregistered service must be inside a `Compile Remove` condition.
