# Capabilities Overview

This section catalogs the workflows and integrations the API template **ships, ready to be configured** — not product features. Your product's business logic is specified in the top-level `docs/core-business/` and `docs/features/` and implemented on top of these capabilities (see [Extending the Template](architecture/EXTENDING_THE_TEMPLATE.md)).

## Shipped Capabilities

### 1. Authentication & Identity
Email/password and Google Sign-In, two-token model (short-lived JWT + rotating refresh tokens with reuse detection), email verification on signup.
*   **See:** [Authentication](security/AUTHENTICATION.md), [Authorization](security/AUTHORIZATION.md)
*   **Configure:** token lifetimes, identity providers, principal types.

### 2. CRUD & Pagination
Standard resource endpoints follow the CQRS feature pattern (one handler per use case) with server-side pagination (`page`, `pageSize`, `PaginatedResponse<T>`), RFC 9457 errors, and automated ownership checks.
*   **See:** [Architecture Overview](ARCHITECTURE_OVERVIEW.md), [Error Handling](architecture/ERROR_HANDLING.md)
*   **Configure:** your aggregates — add slices per [Extending the Template](architecture/EXTENDING_THE_TEMPLATE.md).

### 3. Spatial Queries
Radius search over geo-tagged entities and distance calculations, powered by PostGIS (`geography`, GIST, `ST_DWithin`).
*   **See:** [Spatial Queries & PostGIS](features/SPATIAL_QUERIES.md)
*   **Configure:** which of your entities carry a `Location`; radius clamp.

### 4. Notifications System
A decoupled, asynchronous system for delivering push notifications using a transactional outbox, Postgres `LISTEN/NOTIFY`, and Firebase Cloud Messaging (FCM).
*   **See:** [Notifications](features/NOTIFICATIONS.md)
*   **Configure:** notification types and their triggers; catch-up window; dispatch concurrency.

### 5. Payments (Stripe)
Secure card processing via Stripe Payment Intents against server-issued, single-use order records, with webhook reconciliation, idempotency, and double-spend prevention.
*   **See:** [Stripe Payments Integration](features/PAYMENTS_STRIPE.md)
*   **Configure:** what an "order" represents in your product; fulfillment side effects.

### 6. Aggregated Metrics
Aggregate statistics (counters, totals shown on client dashboards) are computed at the database level with atomic increments to avoid read-modify-write races and expensive client-side queries.
*   **See:** [Security Hardening Checklist](security/audits/SECURITY_HARDENING_CHECKLIST.md) §2 for the atomicity rules.
*   **Configure:** which counters your product tracks.

### 7. Realtime Updates (SSE)
Unidirectional server-to-client event streams over `text/event-stream`, fanned out across instances via Postgres `LISTEN/NOTIFY`.
*   **See:** [ADR 0003](architecture/adrs/0003-server-sent-events-realtime-updates.md)
*   **Configure:** which events stream, and to which resource's audience.

## Your Business Logic

Spec it in `../docs/core-business/` and `../docs/features/` (copy `FEATURE_SPEC_TEMPLATE.md` per feature), then implement it using these capabilities — never rebuild them.
