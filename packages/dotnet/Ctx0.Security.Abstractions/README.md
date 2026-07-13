# Ctx0.Security.Abstractions

Contracts of the ctx.0 server security plane. Referenced by consumer
Domain/Application layers so business code depends only on interfaces:

- Identity/crypto interfaces: `IJwtTokenService`, `IPasswordHasher`,
  `IBlindIndexProvider`, `IGoogleTokenValidator`, `IIdGenerator`,
  `ICurrentUserProvider`, `IDeviceKeyStore`, `IClock`.
- Options: `JwtOptions`, `AleOptions`, `EncryptionOptions`, `RlsOptions`,
  `RbacOptions`, `RoleCatalogSeed`.
- Constants: `CtxProtocol` (wire-protocol version + header),
  `CtxClaimTypes` (JWT claim names).
- `[CtxEncrypted]` — annotate a string entity property (plus an
  `EncryptedDek` property) to opt into envelope encryption; the
  annotation IS the registration.

## Instructions for your LLM / agent

Never re-declare these contracts locally; implement app adapters (e.g.
`IDeviceKeyStore`) in your Infrastructure layer and let `Ctx0.Security`
provide everything else via `AddCtxSecurity`.
