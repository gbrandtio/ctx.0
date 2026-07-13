<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="assets/logo-dark.svg">
    <img src="assets/logo-light.svg" alt="ctx.0" width="396">
  </picture>
</p>

<p align="center"><strong>Build secure applications fast, with minimum tokens burned.</strong></p>

`ctx.0` is a security-first application scaffolding framework designed specifically to be driven by LLM coding agents. It provides a CLI (`ctx0`) that generates a fully integrated Flutter mobile app and a .NET REST API. 

The security pillars arrive as compiled packages you cannot weaken (unless you try hard). Every architectural, security, and performance decision is pre-made and documented in a token-efficient way. This allows agents (and human developers) to focus entirely on business logic, rather than rediscovering how to build a secure CRUD app from scratch.

## What It Offers

`ctx.0` scaffolds a complete client-server architecture:

| Side | Stack | Purpose |
|---|---|---|
| `mobile` | Flutter, **Bloc** state management | Mobile client with all the integrations a CRUD app needs out of the box |
| `api` | .NET, **EF Core (code-first)**, PostgreSQL | REST API designed to pair with the mobile client |

### Out-of-the-box Capabilities

- **Auth**: Email + password with email verification, Google Sign-In, JWT access tokens, and rotating refresh tokens with reuse detection.
- **Navigation variants**: Standard bottom navigation, side rail for tablets/desktop, drawer, top tabs, and pure routes.
- **GDPR & Privacy**: Delete account flow, export data request, and configurable privacy policy / terms of service links within the settings tab.
- **Payments**: Stripe (PaymentIntents + webhooks), Google Pay / Apple Pay.
- **Push notifications**: Firebase Cloud Messaging via a transactional outbox.
- **Maps & location**: Google Maps client-side, PostGIS spatial queries server-side.
- **Caching**: HTTP-level client caching (Hive) and server output caching.
- **Localization & Theming**: Multi-language support, light/dark themes, and offline-first data patterns.

Everything above toggles with `ctx0 enable/disable <id>`.

### Defence in Depth

The security pillar ships as versioned compiled packages the generated apps depend on (e.g., `ctx0_mobile_security` on pub.dev and `Ctx0.Security` on nuget.org). `ctx0 doctor` fails the build if the security plane is removed, overridden, or vendored.

1. **Edge**: TLS, partitioned rate limiting (per-identity / per-IP).
2. **Transport**: Application-layer request/response body encryption (ALE, AES-256-GCM) and per-device ECDSA P-256 request signing.
3. **Identity & access**: Permission-based RBAC, automated resource-ownership (IDOR) checks, short-lived JWTs, refresh token rotation.
4. **Data integrity**: Global input sanitization, atomic operations, server-side source-of-truth validation.
5. **Data at rest**: Envelope encryption (per-row DEKs under a versioned KEK), blind indexes for searchable PII, PostgreSQL Row-Level Security.
6. **Client runtime**: Secure storage, RASP (root/debugger/tamper detection), memory hygiene for secrets.

---

## Installation and Usage

You can easily scaffold a new project using the `ctx0` CLI.

```bash
# 1. Install the CLI globally
dart pub global activate ctx0_cli

# 2. Scaffold a new workspace (Flutter app + .NET API)
ctx0 create workspace acme --org com.acme
cd acme

# 3. Manage integrations
ctx0 status                                 # See what's currently enabled/disabled
ctx0 enable payments_stripe                 # Enable Stripe payments (toggles BOTH sides)

# 4. Verify integrity
ctx0 doctor                                 # Verifies integrity and mobile↔API wire-protocol lock
```

You can also scaffold a single side if needed using `ctx0 create app <name>` or `ctx0 create api <name>`.
Every generated repository includes an `AGENTS.md` file — point your coding agent at it and start building features.

---

## For Developers

This section covers all aspects of working on `ctx.0` itself, its repository structure, and its underlying design principles.

### Repository Structure

Consumers never clone this repository directly; `ctx0 create` is the only supported way to start an application. If you are developing `ctx.0`, here is how the monorepo is structured:

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

### LLM-Facing By Design

`ctx.0` is built from the ground up for AI coding agents:

- **Agent Routers**: `AGENTS.md` files act as routers with task triage, ensuring agents read only the documentation a specific task needs.
- **Locked Security Docs**: `docs/packages/*.md` contain instructions for each installed security package. These are materialized by `ctx0 docs sync` and version-locked to what is actually installed, ensuring agents don't hallucinate security implementations.
- **Fill-in Contracts**: `docs/core-business/` and `docs/features/FEATURE_SPEC_TEMPLATE.md` act as fill-in contracts that your agent consumes before implementing features.

### Contributing

The `templates/` are runnable reference applications. If you are contributing to `ctx.0`:
- Work inside `templates/*` per their respective `AGENTS.md`.
- Ensure `ctx0 doctor`, `flutter test`, and `dotnet test` stay green.
- **Golden Rule**: Any change to the wire protocol (signing string, ALE scheme, security headers) MUST bump the protocol version on **both** security packages and manually regenerate `packages/protocol/wire_protocol_vectors.json`.

### Design Principles

- **Docs are the contract:** Agents must follow the documented patterns instead of guessing. Documentation must be updated in the same change as the code it describes.
- **Secure by default, sealed by default:** The security plane is a compiled dependency. The insecure path requires deliberate effort (explicitly marked plaintext endpoints), and weakening the security plane is not supported.
- **Token-efficient:** Documentation is routed via task triage, kept factual, and avoids duplication so agents burn minimum tokens reading irrelevant context.

## License

Open source and free. See [LICENSE](LICENSE).
