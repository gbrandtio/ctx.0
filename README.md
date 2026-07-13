<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/logo-dark.svg">
    <img src="assets/logo-light.svg" alt="ctx.0" width="396">
  </picture>
</p>

<p align="center"><strong>Build secure applications fast, with minimum tokens burned.</strong></p>

ctx.0 ships production-grade, security-first application scaffolding designed to be driven by LLM coding agents. You generate your app with the `ctx0` CLI; the security pillar arrives as **compiled packages you cannot weaken**; every architectural, security, and performance decision is pre-made and documented in a token-efficient way — so agents (and humans) spend their effort on your business logic, not on rediscovering how to build a secure CRUD app.

## Quick start

```bash
dart pub global activate ctx0_cli

ctx0 create workspace acme --org com.acme   # Flutter app + .NET API, wired together
cd acme
ctx0 status                                 # what's on/off
ctx0 enable payments_stripe                 # toggles BOTH sides
ctx0 doctor                                 # integrity + mobile↔API wire-protocol lock
```

`ctx0 create app <name>` / `ctx0 create api <name>` generate a single side. Every generated repo carries `AGENTS.md` — point your coding agent at it and start speccing features.

## What gets generated

| Side | Stack | Purpose |
|---|---|---|
| `mobile` | Flutter, **Bloc** state management | Mobile client with all the integrations a CRUD app needs out of the box |
| `api` | .NET, **EF Core (code-first)**, PostgreSQL | REST API designed to pair with the mobile client |

### Out-of-the-box capabilities (scaffoldable)

- **Auth**: email + password with email verification, Google Sign-In, JWT access tokens + rotating refresh tokens with reuse detection
- **Payments**: Stripe (PaymentIntents + webhooks), Google Pay / Apple Pay
- **Push notifications**: Firebase Cloud Messaging via a transactional outbox
- **Maps & location**: Google Maps client-side, PostGIS spatial queries server-side
- **Caching**: HTTP-level client caching (Hive) and server output caching
- **Localization**, theming (light/dark), and offline-first data patterns

Everything above toggles with `ctx0 enable/disable <id>` — never by hand-editing.

### Defence in depth (compiled, not scaffoldable)

The security pillar ships as versioned packages the generated apps depend on — [`ctx0_mobile_security`](packages/ctx0_mobile_security/) (pub.dev) and [`Ctx0.Security`](packages/dotnet/Ctx0.Security/), [`Ctx0.Security.Abstractions`](packages/dotnet/Ctx0.Security.Abstractions/), [`Ctx0.Security.EfCore`](packages/dotnet/Ctx0.Security.EfCore/) (nuget.org). Consumers pin or upgrade versions; `ctx0 doctor` fails the build if the plane is removed, overridden, or vendored, and verifies both sides speak the same wire protocol.

1. **Edge**: TLS, partitioned rate limiting (per-identity / per-IP)
2. **Transport (application layer)**: request/response body encryption (ALE, AES-256-GCM with RSA key wrapping) and per-device ECDSA P-256 request signing
3. **Identity & access**: permission-based RBAC with a configurable role catalog, automated resource-ownership (IDOR) checks, short-lived JWTs, refresh token rotation + family revocation
4. **Data integrity**: global input sanitization, atomic operations, server-side source-of-truth validation
5. **Data at rest**: envelope encryption (per-row DEKs under a versioned KEK), blind indexes for searchable PII, PostgreSQL Row-Level Security
6. **Client runtime**: secure storage, RASP (root/debugger/tamper detection), memory hygiene for secrets

## LLM-facing by design

Every generated repo ships its own agent instructions:

- `AGENTS.md` routers with task triage — agents read only the docs a task needs.
- `docs/packages/*.md` — each installed security package's own instructions (what it does, how to configure it, what an agent must never touch), materialized by `ctx0 docs sync` and version-locked to what is actually installed.
- `docs/core-business/` and `docs/features/FEATURE_SPEC_TEMPLATE.md` — fill-in contracts your agent consumes before implementing features.

## Repository structure (this monorepo)

```text
ctx.0/
├── packages/
│   ├── ctx0_cli/                 # The `ctx0` scaffolder (pub.dev)
│   ├── ctx0_mobile_security/     # Mobile security plane (pub.dev)
│   ├── dotnet/Ctx0.Security*/    # API security plane (nuget.org)
│   └── protocol/                 # Golden wire-protocol vectors (both sides test against them)
├── templates/
│   ├── mobile/                   # Source of truth for generated Flutter apps (runnable reference app)
│   └── api/                      # Source of truth for generated .NET APIs (runnable reference API)
├── docs/                         # Fill-in business/feature contracts shipped into generated repos
├── tool/pack_templates.dart      # Embeds templates/ into ctx0_cli at publish time
└── AGENTS.md                     # Contributor router (working on ctx.0 itself)
```

Consumers never clone this repository — `ctx0 create` is the only supported way to start an application.

## Contributing to ctx.0 itself

The templates are runnable reference apps: work inside `templates/*` per their `AGENTS.md`, keep `ctx0 doctor`, `flutter test`, and `dotnet test` green, and remember the golden rule — a change to the wire protocol (signing string, ALE scheme, security headers) must bump the protocol on **both** security packages and regenerate `packages/protocol/wire_protocol_vectors.json` deliberately.

## Design principles

- **Docs are the contract.** Agents must follow the documented patterns instead of guessing; documentation is updated in the same change as the code it describes.
- **Secure by default, sealed by default.** The security plane is a compiled dependency; the insecure path requires deliberate effort (explicitly marked plaintext endpoints) and weakening it is not supported at all.
- **Token-efficient.** Docs are routed via task triage, kept factual, and avoid duplication so agents read only what a task needs.

## License

Open source and free. See [LICENSE](LICENSE).
