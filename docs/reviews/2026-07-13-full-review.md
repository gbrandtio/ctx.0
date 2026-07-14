# ctx.0 Full Codebase Review — 2026-07-13/14

> **Remediation status (2026-07-14).** All Criticals, Highs, and Mediums in this
> report were fixed on branch `fix/full-review-findings` (five commits, one per
> workstream: API, mobile, security-plane/protocol-1.1, scaffolder, CI). Every
> test suite is green, including new proofs: an RLS-engaged payment-flow test,
> an output-cache-hit test, numeric-id `fromJson` tests, protocol-1.1 vectors,
> and a CLI e2e that scaffolds a renamed workspace and compiles both sides.
> Lows are mostly deferred (a few one-liners were fixed opportunistically).
>
> **Newly discovered while fixing C1 — needs a decision (not in the original
> findings):** the API relies on connecting to Postgres as a *superuser* for
> more than payments. Under the documented production posture (a login role that
> is only a member of `app_user`), user **registration** — an anonymous
> `INSERT` into `users` — is denied by `user_self_policy`'s `WITH CHECK
> (id = get_current_user_id())` because there is no current user at signup. The
> integration tests never caught this because `ApiFactory` connects as the
> container superuser (which bypasses RLS). The payments paths were fixed (C1)
> by running them under the internal-worker bypass; the same class of problem
> likely affects other anonymous/cross-cutting writes (registration, refresh,
> app-instance registration). Resolving it properly (bypass on those handlers,
> or deliberate anonymous-insert policies) is a design decision left for the
> maintainer.


Feature-by-feature review of the mobile template, API template, their integration, the security plane packages, and the scaffolder. Report only — no fixes applied. Every Critical/High finding was verified by re-reading the cited code; findings marked *(plausible)* rest on framework semantics not yet exercised empirically.

## Executive summary

The product code is in better shape than the scaffolder. Both app templates build, analyze clean, and pass all their tests (including real Postgres+PostGIS integration tests), the wire protocol artifacts are byte-identical on both sides, and the crypto primitives interoperate correctly. However:

1. **Payments cannot work in production posture** — the RLS policies deny the payment-intent lookup and the anonymous Stripe webhook every write it needs, and the integration tests can't see it because they connect as the Postgres superuser (C1).
2. **The `ctx0` CLI's enable/disable is effectively broken on generated apps** — fresh workspaces record zero enabled features, so `status` lies, `disable` is a silent no-op, and neither auth method can ever be disabled (C2); registry snippets still contain `app_template`/unrenamed placeholders, so post-create enables inject code that cannot compile (C3).
3. **Two runtime crashes ship enabled**: the notifications feed and the map items list both parse the API's numeric `id` with `as String` (H2), and the app-updates gate, once armed server-side, 426-blocks every auth request because the auth client never sends `X-Client-Version` (H5).
4. **Google sign-in ships fail-open** — empty `ClientIds` disables audience validation (H3), and account deletion leaves the Google identity link live, silently resurrecting anonymized accounts on the next Google sign-in (H4).
5. `ctx0 doctor` — the advertised integrity gate — is a stub in the CLI, and the API side has no fallback scaffolder or doctor at all (H6).

## Test suite results

| Suite | Command | Result |
|---|---|---|
| Mobile template | `flutter analyze && flutter test` (+ fallback `scaffold.dart doctor`) | ✅ analyze clean, doctor OK, 44/44 |
| Mobile security package | `flutter test` | ✅ 20/20 |
| API template | `dotnet build && dotnet test` | ✅ build (8 warnings), Unit 31/31, Integration 7/7 (Testcontainers PostGIS) |
| .NET security packages | `dotnet test Ctx0.Security.sln` | ⚠️ **no test projects exist** — protocol/crypto tests live only in the template's UnitTests |
| ctx0 CLI | `dart test` | ❌ **fails to compile** — `test/markers_test.dart` imports nonexistent `lib/src/markers.dart` |

Caveat on the green API integration tests: `tests/IntegrationTests/ApiFactory.cs:20-25` connects as the Testcontainers-created superuser, which **bypasses RLS entirely** — none of the API-through-handler tests exercise the row-level-security posture the template deploys with.

---

## Critical

### C1 — Payment flows are denied by the shipped RLS policies (API)
`Infrastructure/Persistence/Migrations/20260710195831_AddRowLevelSecurity.cs:74-93`, `Application/Features/Payments/CreatePaymentIntent.cs:23`, `AppApi/Endpoints/v1/PaymentsEndpoints.cs:38-80`

- `orders` policies require `is_project_member(project_id)`; a consumer paying for their order is a `users` identity, not a project member → the order lookup 404s.
- The Stripe webhook is `AllowAnonymous`; `RlsInterceptor` sets an empty user → `get_current_user_id()` is NULL → the order SELECT is denied, `TryMarkPaidAsync` updates 0 rows, and the `ledger` INSERT is denied outright (`ledger` has a SELECT-only policy for `app_user`). `user_notifications` INSERT fails its WITH CHECK.
- No payments path uses `BeginSystemBypassScope()` (only `PostgresNotificationListener` and `KekRotationWorker` do).

**Failure scenario**: first deployment that follows `CtxRls.cs`'s own instruction to connect as a member of `app_user` — every payment intent 404s and every webhook is a no-op.
**Remediation**: run the webhook (and the intent's order lookup) inside a system-bypass scope with an explicit transaction, or add deliberate policies; add one RLS-engaged integration test that drives the API as a non-superuser role.

### C2 — `ctx0 create` records no enabled features, breaking enable/disable/status on every generated app (CLI)
`packages/ctx0_cli/lib/src/create.dart:91-92, 277-278`; `lib/src/injector.dart:44,50`; `lib/src/commands.dart:89-104`

Generated repos get `{"kind": …, "enabledFeatures": []}` although the mobile template ships with `auth_google`, `auth_email_password`, `profile`, `settings`, `nav_bottom` live and the API template ships **everything** enabled. Consequences:
- `ctx0 status` reports everything disabled.
- `ctx0 disable <shipped-enabled id>` early-returns as a no-op but still prints `✓ … disabled. Code ejected.`
- The last-auth-method guard blocks disabling **either** auth method (neither is in the set), so `ctx0 disable auth_google` errors with "auth_email_password is already disabled" on a fully-default app.

**Remediation**: write the actual shipped-enabled feature set into `workspace.json` at create time (derive it from the template's marker state or a canonical manifest).

### C3 — Registry injection payloads contain unrenamed template placeholders (CLI/registry)
`registry/mobile/features/auth_google/integration.json:48`, `auth_email_password/integration.json:44-45`, `registry/mobile/features/{image_capture,auth_email_password}/test/**` (`package:app_template/...`)

`createApp` rewrites `app_template` → `<name>` over the copied tree (`create.dart:59-67`), but registry snippets/sources are injected verbatim — both by `--with` at create time (which runs **after** `_rewriteTree`, `create.dart:104-110`) and by any later `ctx0 enable`. Enabling `auth_google`, `auth_email_password`, or `image_capture` in an app not literally named `app_template` injects `import 'package:app_template/...'` → the app no longer compiles. The API registry equivalently carries `AppApi` namespaces (`registry/api/features/*/integration.json`) that break renamed APIs.

**Remediation**: apply the same placeholder rewrite to snippets and copied sources at injection time (the manifest already knows the app name), or store snippets with explicit placeholders and substitute on inject.

---

## High

### H1 — Stripe webhook fulfillment is not atomic (API)
`Application/Features/Payments/ProcessPaidPaymentIntent.cs:49-76`. `TryMarkPaidAsync` commits immediately (ExecuteUpdate), then ledger and notification are two further separate commits, no transaction. Crash between order-update and ledger write → Stripe redelivers → handler sees `paid` and returns → **ledger entry and notification are lost permanently**. The XML comment ("same transaction scope") and the unit-test name (`…atomically`, mock-based) overstate. **Fix**: one `BeginTransactionAsync` around the handler (all repos share the scoped `AppDbContext`).

### H2 — Mobile crashes parsing numeric ids: notifications feed and map items (mobile↔API contract)
`templates/mobile/lib/models/app_notification.dart:18` and `lib/models/item.dart:18` do `json['id'] as String`; the API serializes `long Id` (`Contracts/Notifications/NotificationContracts.cs:4`, `Contracts/Items/ItemContracts.cs:4`) as a JSON number, and no serializer override exists. Every real fetch of `/v1/users/notifications` or `/v1/items/nearby` throws `TypeError` — both features cannot display live data. Invisible to tests because mocks use String ids. `user.dart:17`/`auth_session.dart:23` already use the correct `.toString()` pattern. **Fix**: `json['id'].toString()` + a `fromJson` test each.

### H3 — Google ID-token audience validation is disabled by default (API/security package)
`packages/dotnet/Ctx0.Security/GoogleTokenValidator.cs:24` passes `Audience = null` when `ClientIds` is empty; `templates/api/AppApi/appsettings.json:34` ships `"ClientIds": []` and `auth_google` ships enabled. Any Google-issued ID token minted for **any** OAuth client is accepted. **Fix**: fail closed — startup error when Google auth is wired with empty `ClientIds`.

### H4 — Deleted (anonymized) accounts resurrect via Google sign-in (API, GDPR)
`Application/Features/Users/DeleteUser.cs:21-45` removes the FCM identity and revokes refresh tokens but leaves `UserGoogleIdentity`; `Google/AuthenticateGoogleUser.cs:32` looks up by subject hash and issues tokens with no `IsAnonymized` check. A GDPR-deleted user's next Google sign-in silently logs into the gutted `deleted-{id}` account — and the retained subject-hash link is itself un-erased personal data. **Fix**: delete Google identity rows in `DeleteUserHandler`; reject `IsAnonymized` users at every login path.

### H5 — App-update gate 426-blocks all auth traffic and never shows the upgrade UI (mobile)
`templates/mobile/lib/main.dart:62-77`: `AuthRepository` uses raw `apiFactory.client`; only the module-facing `cachingClient` is wrapped in `VersionCheckClient` (which adds `X-Client-Version` and observes 426). `VersionCheckMiddleware.cs:22-24` rejects requests missing the header once `MINIMUM_CLIENT_VERSION` is set. Result: login/signup/refresh/logout get 426 even on up-to-date apps, surfaced as generic login failure (the overlay never triggers). Internal refreshes from `AuthRefreshClient` are equally unwrapped. **Fix**: put the version header inside the security chain (or wrap both clients).

### H6 — `ctx0 doctor` is a stub; the API side has no doctor at all (CLI)
`packages/ctx0_cli/lib/src/commands.dart:118-122` prints `doctor: OK` unconditionally. README/AGENTS advertise doctor as the integrity gate ("fails the build if the security plane is removed"). Only the mobile template's fallback `tool/scaffold.dart` implements real checks; **`templates/api/` has no `tool/` directory** — generated API repos have no integrity check whatsoever. `docsDriftProblems` (docs_sync.dart) is documented as "consumed by ctx0 doctor" but never called. The workspace-level protocol lock (`workspace.dart:119-152`) is the only real check and it works.

### H7 — CLI cannot toggle documented integrations; unknown ids crash with a stack trace (CLI/registry)
Registry lacks `auth_2fa_email` (both sides), `email_brevo` (API), and all six nav variants (mobile) — all present in the template catalogs and README ("Everything above toggles with `ctx0 enable/disable`"). `catalog.dart:35-36` hardcodes `authMethodIds`/`navMethodIds` ("Hardcoded for now"). `ctx0 enable nav_rail` or `auth_2fa_email` throws an uncaught `StateError` from `Catalog.byId`.

### H8 — Stale bundled template payload silently shadows the canonical templates (CLI)
`packages/ctx0_cli/templates/` (a gitignored `pack_templates.dart` artifact, present in this checkout) is preferred by `resolveTemplateDir` (`create.dart:149-158`) over `templates/`. The current local copy predates 2FA (`Authenticate2FA.cs`, `SendTwoFactorCode.cs` missing), app_updates (`VersionCheckMiddleware.cs` missing), and current docs — `ctx0 create` run from this machine scaffolds old code with no warning. Also a publish-process risk: nothing verifies the pack step ran before `dart pub publish`. **Fix**: prefer the repo templates when running from a checkout, print which payload was used, and gate publishing on a freshness check.

---

## Medium

| # | Area | Finding |
|---|---|---|
| M1 | Auth/GDPR | `HasTrackingConsent` is persisted (`UpdateUser.cs:23-25`) but **never returned** — `UserResponse` (Contracts/Users/UserContracts.cs) lacks the field; mobile reads `json['hasTrackingConsent']` → always null; the Settings toggle visibly reverts after a successful save. |
| M2 | Auth/GDPR | `RegisterUserRequest.Consents` is sent by mobile but `RegisterUser.cs:27-64` never reads it — signup consents silently discarded. |
| M3 | GDPR | Consent banner fires pre-login: `gdpr_banner_overlay.dart:30-50` fire-and-forgets `updateProfile`, which fails with "No active session" and is discarded; nothing replays the stored choice after login — client and server consent state disagree permanently. |
| M4 | GDPR | Data export dead-ends: `RequestUserExport.cs:19-29` writes a `Pending` row; no worker, download endpoint, or notification fulfills it, while the mobile copy promises push delivery (`settings_cubit.dart:55`). GDPR Art. 20 appears implemented but isn't. |
| M5 | Payments | Stripe idempotency key `payment-intent:{orderId}` (`StripePaymentGateway.cs:52`) is shared across users → second payer gets a Stripe `idempotency_error` → 500; webhook attribution metadata frozen to the first caller for 24 h. |
| M6 | Push | No redelivery after transient FCM failure or LISTEN downtime: `PostgresNotificationListener.cs:37` catch-up runs at startup only; the reconnect loop doesn't re-run it; failed sends stay `sent_at NULL` until the next restart. |
| M7 | Security plane | Cross-user cache leak on session expiry: Hive cache keyed on raw URL (`caching_client.dart:27`); logout purges it but `AuthRefreshClient._expireSession` (`auth_refresh_client.dart:97`) does not → next account on a shared device is served the prior user's cached responses (15-min TTL). |
| M8 | Security plane | HTTP response cache unencrypted at rest: `hive_cache_service.dart:23` opens the box without `encryptionCipher`, yet authenticated per-user bodies (profile, notifications) are cached — PII in plaintext on device. |
| M9 | Security plane | Request signature covers path only (`RequestSigningMiddleware.cs:83`, `secure_device_signing_client.dart:52`); GETs also skip ALE → query params (lat/lng, cursors) are tamperable in transit. Protocol gap (both sides agree); fixing needs a protocol bump. |
| M10 | Security plane | Replay protection is a 300 s timestamp window with no nonce store (`RequestSigningMiddleware.cs:49-54`) — captured signed non-idempotent POSTs can be replayed for up to 5 minutes. |
| M11 | Maps/caching *(plausible)* | The `items-nearby` output-cache policy (`Program.cs:37-41`) is layered on the default policy, which refuses to cache requests with an `Authorization` header — and the endpoint requires auth. The documented 30 s cache likely never stores anything (fail-safe, but dead). Verify empirically; fix with `excludeDefaultPolicy: true`. |
| M12 | Onboarding/scaffolder | Permission slides are unreachable: `onboarding_screen.dart:44-52,122-189` blocks are `ctx:off` although maps/push/image-capture ship enabled, and the file is absent from every integration's `markedFiles` — no toggle ever re-enables them; doctor never sees them. |
| M13 | app_updates | `VersionCheckMiddleware` runs before `MapHealthChecks` with no exemption (`Program.cs:66,76`) → once `MINIMUM_CLIENT_VERSION` is set, health probes get 426 and instances are marked unhealthy. |
| M14 | Scaffolder | `templates/api/.ctx/integrations.json` is invalid JSON (trailing comma in the `app_updates` entry, ~line 92) — ships broken into every generated API repo; `docs/INTEGRATIONS.md` presents it as the manifest. |
| M15 | Scaffolder | Dual-engine incoherence: generated mobile apps carry both the fallback comment-toggle engine (`tool/scaffold.dart` + `.ctx/integrations.json`, state = `ctx:off` markers) and the CLI registry engine (state = `workspace.json`); neither updates the other. A CLI eject empties marker blocks, after which the fallback can never re-enable (its model is uncomment, not re-insert). |
| M16 | CI | No CI runs `dotnet build/test` for the API template, `dart test` for the CLI (the broken suite would have been caught), any `ctx0 create` e2e, or the .NET security packages. Only the mobile fallback paths are covered (`mobile-integrations.yaml`). |

## Low

- **L1** Auth: signup and 2FA codes share `SignupVerification` with no `Purpose` column — a code minted for one flow satisfies the other for the same email within its lifetime (`SendSignupCode.cs`, `SendTwoFactorCode.cs:31`, both resolved via `FindActiveByEmailHashAsync`).
- **L2** Auth: concurrent refresh with the same token yields two live tokens in one family (`RefreshUserToken.cs:30-57`, read-check-revoke without a conditional update); benign double-submit case only.
- **L3** Auth: plaintext password held in Bloc state across the 2FA step (`login_state.dart:31-38`) and replayed to `/authenticate/2fa` — inherent to the API design; a challenge token would be cleaner.
- **L4** Payments: duplicate webhook deliveries re-broadcast the SSE `payment_completed` event (`PaymentsEndpoints.cs:71-73` fires `pg_notify` even when the handler no-opped).
- **L5** Push: single FCM token per user — a second device overwrites the first, and unregister deletes by user id, so logging out on device A kills push on device B.
- **L6** GDPR: `DeleteUser` leaves `UserNotification` Title/Body rows (may embed personal data); access tokens stay valid until expiry after delete (acceptable, document it).
- **L7** Security plane: RLS `FORCE` doesn't cover superuser/`BYPASSRLS` login roles (`CtxRls.cs:54-58`) — deployment invariant, document/enforce; `SnowflakeIdGenerator` NodeId defaults to 0 → multi-instance id collisions; canonical-string trim asymmetry (server trims body, mobile doesn't).
- **L8** Injector: positional snippet↔marker matching with no count validation; missing marked files silently skipped (`injector.dart:87-119`).
- **L9** Registry drift: `registry/mobile/features/auth_email_password/test/.../signup_flow_test.dart` differs from the template copy (everything else matches byte-for-byte).
- **L10** Mobile: onboarding strings hardcoded English in an otherwise EN+ES-localized app; `ApiConstants.useMockData` referenced nowhere; `nav_tabs` variant's `DefaultTabController(initialIndex:)` desyncs on programmatic branch changes.

## Notes

- 2FA gates only password login, not Google/registration — likely intentional; document it. Stale comment in `google_auth_service.dart:11` (old route name). `UpdateUser.cs:22` can never clear `Name`.
- CLI UX: `cmdEnable`/`cmdDisable` print success (and run `flutter pub get`) even when the toggle no-opped; `cmdDisable` assumes exactly two auth methods; `lib/src/update_create.dart` is leftover migration code inside the published package.
- Build artifacts are checked into the working tree (`templates/mobile/{build,.dart_tool,android/.gradle}`, `templates/api/**/{bin,obj}`).
- `RoleCatalog.cs:61` direct-role fallback grants a JWT whose `role` equals a grant-role name that role's permissions — safe today, worth a guard. `AleMiddleware.cs:59` skips decryption for chunked (null Content-Length) request bodies — not exploitable today.
- Webhook silently drops events with missing/malformed metadata (`PaymentsEndpoints.cs:61-65`, returns 200 with no logging).

## What's sound

Verified working as designed: wire-protocol lock and vectors (all three copies byte-identical, `1.0` both sides, RSA-OAEP-SHA256/MGF1 and AES-GCM layouts interoperate); ALE and signing crypto primitives; envelope-encryption interceptor (per-row DEK, KEK-version upgrade on save); refresh single-flight; parameterized RLS `set_config`; BCrypt + constant-time `DummyVerify`; blind-index key separation; partitioned rate limiter; refresh rotation with family reuse detection; PostGIS query hygiene (geography column, GIST index, clamps, `Take(100)`, `AsNoTracking`); marker-block integrity for auth/payments/push/maps on both sides; profile feature (SSOT via auth stream, IDOR-safe `UserSelf` policies); mobile delete-account purge; workspace protocol-lock doctor.

## Contract-consistency matrix

| Feature | Mobile call | API endpoint | Verdict |
|---|---|---|---|
| Auth: send signup code | POST `/v1/users/register/send-code` | 202 | OK |
| Auth: register | POST `/v1/users` | AuthResponse | Wire OK; `consents` ignored (M2) |
| Auth: login | POST `/v1/users/authenticate` | tokens / `requiresTwoFactor` | OK |
| Auth: 2FA | POST `/v1/users/authenticate/2fa` | AuthResponse | OK |
| Auth: Google | POST `/v1/users/google/authenticate` | AuthResponse | OK |
| Auth: refresh / logout | POST `/v1/users/refresh`, `/logout` | AuthResponse / 204 | OK |
| Users: get / update | GET/PATCH `/v1/users/{id}` | UserResponse | Response lacks `hasTrackingConsent` (M1) |
| Users: delete / export | DELETE `/v1/users/{id}`, POST `…/exports` | 204 / 202 | Delete OK; export never fulfilled (M4) |
| Payments: intent | POST `/v1/payments/intents` | `{clientSecret}` | OK (string→long coercion fine) |
| Payments: webhook | POST `/v1/payments/stripe-webhook` | — | Blocked by RLS in prod posture (C1) |
| Push: register/unregister token | POST/DELETE `/v1/users/firebase/token` | 204 | OK |
| Push: feed | GET `/v1/users/notifications` | PagedResponse | **MISMATCH — id crash (H2)** |
| Maps: nearby | GET `/v1/items/nearby` | ItemResponse[] | **MISMATCH — id crash (H2)** |
| app_updates | `X-Client-Version` header | 426 + ProblemDetails | **MISMATCH — auth client unwrapped (H5)** |

## Drift appendix

- **Registry vs templates**: byte-identical except one file (L9).
- **Bundled CLI payload vs canonical templates**: badly stale locally (missing 2FA, app_updates middleware, doc updates) and silently preferred (H8). Gitignored, so a fresh clone is unaffected — but any machine that ever ran `pack_templates.dart` scaffolds from that snapshot.
- **Catalogs**: template `.ctx/integrations.json` (17 mobile ids incl. nav + 2FA; 7 API ids) vs registry (10 mobile / 5 API ids) — the CLI can't reach 7 mobile and 2 API integrations (H7); the API catalog file is invalid JSON (M14).
- **Docs vs code**: README claims universal `ctx0 enable/disable` and a security-plane-verifying doctor (both false today, H6/H7); CACHING_STRATEGY.md documents an output cache that likely never stores (M11); `ProcessPaidPaymentIntent` doc-comment claims a transaction scope that doesn't exist (H1); `docs_sync` doctor integration documented but unwired.

## Suggested priority order

1. C2 + C3 (CLI is the product's front door; both break every generated app)
2. C1 + H1 (payments correctness under production posture)
3. H2 + H5 (shipped runtime crashes / lockout)
4. H3 + H4 (Google auth fail-open + GDPR resurrection)
5. H6–H8 (doctor, registry completeness, payload staleness)
6. M1–M4 (GDPR/consent coherence), then the remaining Mediums
7. CI gaps (M16) — a `dotnet test` + `dart test` + `ctx0 create` e2e workflow would have caught C2, C3, H2, and the broken CLI tests
