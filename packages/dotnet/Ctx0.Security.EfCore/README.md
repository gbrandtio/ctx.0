# Ctx0.Security.EfCore

EF Core adapters of the ctx.0 server security plane.

## What it does

- `EnvelopeEncryptionInterceptor`: transparent envelope encryption for
  PII — per-row DEKs wrapped by the versioned KEK, AES-256-GCM with
  per-operation nonces. Discovers `[CtxEncrypted]` properties by
  reflection; no central registry, no knowledge of your entities.
- `RlsInterceptor`: sets the Postgres RLS identity
  (`RlsOptions.SettingName`) with a parameterized, transaction-local
  `set_config` before every command; background workers use the system
  bypass (`SET LOCAL ROLE <RlsOptions.WorkerRole>`).
- `CtxRls`: migration helpers (roles, `get_current_user_id()`,
  ENABLE/FORCE, owner policies, worker bypass) emitting SQL
  version-locked to the interceptor.

## Instructions for your LLM / agent

- Register both interceptors on your DbContext
  (`options.AddInterceptors(...)`); register
  `EnvelopeEncryptionInterceptor` as a singleton and `RlsInterceptor` as
  scoped.
- New PII entity: annotate string properties with `[CtxEncrypted]`, add a
  string `EncryptedDek` property, add blind-index `*_hash` companions.
- New user-owned table: use `CtxRls.EnableForce` +
  `CtxRls.OwnerPolicy`/hand-written policies + `CtxRls.WorkerBypass` in
  the same migration. Never inline the RLS plumbing SQL by hand.
- Any change to the interceptor call must preserve parameterization and
  transaction-local scope.
