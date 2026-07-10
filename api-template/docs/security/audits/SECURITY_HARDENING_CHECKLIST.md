# Security Hardening Checklist

This document records the vulnerability classes this template explicitly defends against and the controls that implement each defense. **Never regress these controls.** When fixing bugs or refactoring, cross-check your change against this list; when a new vulnerability class is discovered and fixed, add it here.

## 1. Client-Supplied Transaction Parameters ("Double Truth")

**Class:** Critical — parameter tampering.
**Threat:** A handler that trusts `Amount`, `ProjectId`, or similar values from the request body can be exploited even when a server-issued artifact (e.g., a single-use order record) is also referenced: an attacker holds a server-issued €5 order but submits a request claiming €0.05.
**Control:** Handlers parse the authoritative transaction data **from the validated server-side record** (e.g., the `orders` row created server-side). Client-provided copies of those parameters are ignored. There must be exactly one source of truth, and it must live server-side.

## 2. Database Race Conditions (Double Spending & Lost Updates)

**Class:** High — concurrency.
**Threat:**
- **Double Spending:** Concurrent requests process the same single-use order because the "is it used?" check and the "mark used" update are separate statements.
- **Lost Updates:** Counters (points, aggregate totals) computed in memory (Read-Modify-Write) overwrite each other under concurrency.
**Control:**
- **Atomic Invalidation:** `TryInvalidateAsync` uses `ExecuteUpdateAsync` to atomically check-and-flip the `IsInvalidated` bit; if the bit was already flipped, the operation fails and the transaction is rejected.
- **Atomic Increments:** All counter mutations use `ExecuteUpdateAsync` set-based SQL (`SET points = points + @delta`) — never read-modify-write.

## 3. Broken Access Control (IDOR)

**Class:** High — authorization.
**Threat:** Users read or modify resources belonging to others by guessing IDs.
**Control:** Three independent layers:
1. Authorization policies with automated resource handlers matching JWT claims against route parameters (see [Authorization](../AUTHORIZATION.md) — including the mandatory route parameter naming conventions).
2. Explicit ownership checks in handlers where the relationship is hierarchical.
3. PostgreSQL RLS as the fail-safe (see [Row-Level Security](../ROW_LEVEL_SECURITY.md)).
Any new endpoint touching owned resources must use the standard policies; never rely on "the client won't send someone else's ID".

## 4. User Enumeration via Timing Attack

**Class:** Medium — information disclosure.
**Threat:** Authentication returns immediately for unknown accounts but runs a slow BCrypt verification for existing ones, leaking account existence via response time.
**Control:** When the user is not found, a "dummy" BCrypt verification against a fixed hash is performed so execution time is consistent (slow) regardless of account existence.

## 5. Sensitive Data Exposure in Logs

**Class:** Medium — data leakage.
**Threat:** PII (passwords, emails, names, tokens) leaking into structured logs through nested objects, dictionaries, or collections.
**Control:** `SensitiveDataDestructuringPolicy` (Serilog) recursively masks sensitive keys in dictionaries, collections, and nested object properties. New DTO fields carrying PII must be added to the masked-key list.

## 6. Transport-Layer Hardening Beyond TLS

**Class:** Proactive defense-in-depth.
**Controls:**
- **Application-Layer Encryption (ALE):** Transparent AES-256-GCM encryption for request and response bodies. Protects against TLS termination attacks.
- **Request Signing (ECDSA P-256, per-device):** Mandatory asymmetric signatures for all requests with a 5-minute timestamp validity window to neutralize tampering and replay attacks. (An earlier HMAC shared-secret design was **rejected**: a secret shipped inside the app binary can be extracted by decompilation. Do not reintroduce shared-secret signing.)
- **Security Metadata Endpoint:** `/v1/security/metadata` lets the mobile app dynamically fetch SSL fingerprints, the ALE public key, and current security policies.
- **Device Attestation:** Infrastructure for verifying Google Play Integrity and Apple App Attest tokens.
See [Application-Layer Security](../APPLICATION_LAYER_SECURITY.md).

## 7. RLS Interceptor SQL Injection & Session Leak

**Class:** Critical — injection / privilege escalation.
**Threat:** Passing the identity to PostgreSQL via string interpolation (`SET app.current_user_id = '{userIdValue}'`) opens a SQL injection vector through the identity claim. Using a session-wide `SET` (instead of a transaction-local setting) in a connection-pooling environment leaks the identity into unrelated requests reusing the connection — horizontal privilege escalation.
**Control:** The `RlsInterceptor` uses a **parameterized**, **transaction-local** call: `SELECT set_config('app.current_user_id', @userId, true)` executed as a separate command on the same connection/transaction. See [RLS Deep Dive](../RLS_DEEP_DIVE.md). Any change to the interceptor must preserve both properties (parameterization + transaction-local scope).

## 8. Overbroad RLS Policies (Horizontal Privilege Escalation)

**Class:** High — authorization.
**Threat:** Monolithic `FOR ALL` policies grant write access where only read was intended (e.g., members editing other members' records); global `USING (true)`-style read policies expose other tenants' orders.
**Control:**
- Policies are split into explicit, granular operations (`FOR SELECT`, `FOR INSERT`, `FOR UPDATE`, `FOR DELETE`).
- Member policies: members can only read/update their own profile; only Org Admins can insert/delete.
- Single-use order reads are scoped strictly to the owning project's members.
- `SECURITY DEFINER` helper functions set `SET search_path = public` to prevent search-path hijacking.
See [Database RLS Policies](../DATABASE_RLS_POLICIES.md).

## 9. Insecure Password Update Logic

**Class:** High — account takeover.
**Threat:** Profile-update handlers that accept a new password without proof of the old one let a session hijacker permanently lock out the legitimate owner.
**Control:** Password changes require an `OldPassword` field which is verified against the stored BCrypt hash before the update is applied; a successful change revokes all active refresh-token sessions. Password changes live in a dedicated endpoint (`/change-password`), never inside general profile updates.

## 10. Missing Database Integrity Constraints

**Class:** Medium — data integrity.
**Threat:** Missing foreign keys allow orphaned records and fabricated rows pointing at non-existent parents (e.g., ledger entries for non-existent projects or orders).
**Control:** Every relationship is enforced with a Foreign Key constraint, declared in the EF Core model so migrations generate them automatically (see [Database Code-First Guide](../../architecture/DATABASE_CODE_FIRST.md)). New entities must declare their navigation/FK configuration; never rely on application-level integrity alone.

## 11. Unprotected Authentication Endpoints

**Class:** Medium — brute force / abuse.
**Threat:** A single global rate limit leaves login and registration endpoints vulnerable to credential stuffing, password brute-forcing, and mass bot registration.
**Control:** Dedicated rate-limiting policies on top of the global limit:
- **`auth`:** 200 requests / 5 minutes per IP — applied exclusively to `/authenticate` and `/refresh` endpoints.
- **`account_creation`:** 50 requests / 1 hour per IP — applied exclusively to registration endpoints.
New authentication-adjacent endpoints (password reset, OTP dispatch) must be attached to one of these policies.
