<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/logo-dark.svg">
    <img src="assets/logo-light.svg" alt="ctx.0" width="396">
  </picture>
</p>

<p align="center"><strong>Build secure applications fast, with minimum tokens burned.</strong></p>

ctx.0 is a pair of production-grade, security-first application templates designed to be driven by LLM coding agents. Every architectural, security, and performance decision is pre-made and documented in a token-efficient way, so agents (and humans) spend their effort on your business logic and not on rediscovering how to build a secure CRUD app.

## What's inside

| Template | Stack | Purpose |
|---|---|---|
| [`mobile-template/`](mobile-template/) | Flutter, **Bloc** state management | Mobile client with all the integrations a CRUD app needs out of the box |
| [`api-template/`](api-template/) | .NET, **EF Core (code-first)**, PostgreSQL | REST API designed to pair with the mobile template |

### Out-of-the-box capabilities

- **Auth**: email + password with email verification, Google Sign-In, JWT access tokens + rotating refresh tokens with reuse detection
- **Payments**: Stripe (PaymentIntents + webhooks), Google Pay / Apple Pay
- **Push notifications**: Firebase Cloud Messaging via a transactional outbox
- **Maps & location**: Google Maps client-side, PostGIS spatial queries server-side
- **Caching**: HTTP-level client caching (Hive) and server output caching
- **Localization**, theming (light/dark), and offline-first data patterns

### Defence in depth

Both templates implement layered security, documented in detail:

1. **Edge**: TLS, partitioned rate limiting (per-identity / per-IP)
2. **Transport (application layer)**: request/response body encryption (ALE, AES-256-GCM with RSA key wrapping) and per-device ECDSA P-256 request signing
3. **Identity & access**: permission-based RBAC with a configurable role catalog (default roles `Admin`, `ReadWrite`, `ReadSelf`, `Payments`; custom roles via config), automated resource-ownership (IDOR) checks, short-lived JWTs, refresh token rotation + family revocation
4. **Data integrity**: global input sanitization, atomic operations, server-side source-of-truth validation
5. **Data at rest**: envelope encryption (per-row DEKs under a versioned KEK), blind indexes for searchable PII, PostgreSQL Row-Level Security
6. **Client runtime**: secure storage, RASP (root/debugger/tamper detection), memory hygiene for secrets

## Repository structure

```text
ctx.0/
├── AGENTS.md                     # Top-level router: sends agents to the right template
├── docs/
│   ├── core-business/            # Fill-in templates for YOUR product's business context
│   └── features/                 # Fill-in specs for YOUR features (copy FEATURE_SPEC_TEMPLATE.md)
├── mobile-template/
│   ├── AGENTS.md                 # Agent routing guide — start here
│   └── docs/
│       ├── FLUTTER_ARCHITECTURE.md, STATE_MANAGEMENT.md, CODING_STANDARDS.md, ...
│       ├── APP_SHELL.md          # Navigation, headers, settings, GDPR + FeatureModule contract
│       └── brand-kit/            # Drop YOUR branding assets here
└── api-template/
    ├── AGENTS.md                 # Agent routing guide — start here
    └── docs/
        ├── ARCHITECTURE_OVERVIEW.md, SECURITY_OVERVIEW.md, PERFORMANCE_OVERVIEW.md
        ├── architecture/         # Error handling, middleware, code-first DB, extension recipe, ADRs
        ├── security/             # AuthN/AuthZ, RLS, envelope encryption, hardening checklist
        ├── performance/          # EF Core performance, caching strategy
        └── features/             # Spatial queries, notifications, Stripe payments
```

## How to use a template

1. **Copy** the template folder into your new repository.
2. **Rename the placeholder**: the docs use `App` as a global placeholder (`AppApi`, `AppDbContext`, `X-App-Signature`, `app_user`, ...). Rename it to your product name consistently.
3. **Fill in the business context**: complete `docs/core-business/BUSINESS_CASE.md` and `CLIENT_SPECS.md` because AI agents read these before implementing features.
4. **Spec your features**: for each business feature, copy `docs/features/FEATURE_SPEC_TEMPLATE.md` to `docs/features/<FEATURE>.md` and fill it in. The templates ship the capabilities (auth, payments, maps, notifications, security, app shell); your features configure and build on them.
5. **Add your branding**: put logos, icons, and palette assets in `mobile-template/docs/brand-kit/` (see its README).
6. **Point your agent at `AGENTS.md`** in the template root. It routes every task to the minimum set of docs required keeping the token budget small.

## Design principles

- **Docs are the contract.** Agents must follow the documented patterns instead of guessing; documentation is updated in the same change as the code it describes.
- **Secure by default.** The insecure path should require deliberate effort (e.g., explicitly marked plaintext endpoints).
- **Token-efficient.** Docs are routed via task triage, kept factual, and avoid duplication so agents read only what a task needs.

## License

Open source and free. See [LICENSE](LICENSE).
