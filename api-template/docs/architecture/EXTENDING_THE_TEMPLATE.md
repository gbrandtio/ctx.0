# Extending the Template: Add-a-Feature Recipe

The API is organized as vertical slices behind explicit registration points. Adding a business feature never modifies shipped subsystems — it plugs into them. This is the end-to-end recipe; each step links the binding doc.

## 0. Spec first

Copy `../../../docs/features/FEATURE_SPEC_TEMPLATE.md` → `docs/features/<FEATURE>.md` (top-level repo `docs/`) and fill it in: actors & permissions, endpoints & DTOs, data & RLS, caching. Agents must not implement unspecced features.

## 1. Domain entity + EF configuration

- Add the entity under `Domain/Entities/`; every relationship gets a navigation/FK configuration so migrations generate the constraints ([Database Code-First Guide](DATABASE_CODE_FIRST.md), [Hardening Checklist](../security/audits/SECURITY_HARDENING_CHECKLIST.md) §10).
- PII fields: envelope encryption value converters + blind-index `*_hash` companions ([Envelope Encryption](../security/ENVELOPE_ENCRYPTION_ARCHITECTURE.md)).
- Geo fields: `Point` mapped to `geography`, GIST index ([Spatial Queries](../features/SPATIAL_QUERIES.md)).

## 2. Migration + RLS in the same migration

If the table holds user- or tenant-owned data, the migration must ship (per [Database RLS Policies](../security/DATABASE_RLS_POLICIES.md) §4):
1. `ENABLE`/`FORCE ROW LEVEL SECURITY` + granular per-operation policies.
2. The `internal_worker_bypass_<table>` policy.
3. An update to the RLS policy catalog doc.

## 3. Application layer (CQRS handlers)

One folder per aggregate, one file per use case under `Application/Features/<Aggregate>/` ([Architecture Overview](../ARCHITECTURE_OVERVIEW.md)). Repository interfaces go in `Application/Abstractions/`. Follow the atomicity rules for counters/single-use records ([Hardening Checklist](../security/audits/SECURITY_HARDENING_CHECKLIST.md) §2).

## 4. Permissions & policies

New `resource:action` constants in `Domain.Constants.SecurityConstants`, mapped in `PermissionHandler`, policy registered in `SecurityExtensions` ([Authorization](../security/AUTHORIZATION.md) §8). Routes must use the standard resource-ID parameter names so automated IDOR handlers engage (§5).

## 5. Endpoints (one `IEndpointModule` per aggregate)

```csharp
public interface IEndpointModule { void Map(IEndpointRouteBuilder v1); }
```

Add `<Aggregate>Endpoints` under `AppApi/Endpoints/v1/` and register it in the module list in `Program.cs` — the **only** shipped file you touch besides `SecurityConstants`. Group-level filters give the module the full security pipeline (sanitization, signing, ALE, rate limiting) for free ([Filters & Middleware](FILTERS_AND_MIDDLEWARE.md)). Apply `.RequireAuthorization(...)` policies; model public cacheable reads as `GET` + output caching ([Caching Strategy](../performance/CACHING_STRATEGY.md)).

## 6. Cross-cutting hooks (as the spec requires)

- **Push notifications**: write to the outbox in the same transaction ([Notifications](../features/NOTIFICATIONS.md)).
- **Payments**: create server-side `orders` rows; never trust client amounts ([Payments](../features/PAYMENTS_STRIPE.md)).
- **Realtime**: SSE per [ADR 0003](adrs/0003-server-sent-events-realtime-updates.md).

## 7. Docs + contract in the same change

Update the affected docs (repo rule: docs are the contract) and re-export the OpenAPI spec to `mobile-template/docs/API/swagger.json` if endpoints changed.

## Never do

- Weaken a control from the [Security Hardening Checklist](../security/audits/SECURITY_HARDENING_CHECKLIST.md).
- Bypass the registration points by editing shipped subsystems.
- Ship a table holding owned data without RLS in the same migration.
