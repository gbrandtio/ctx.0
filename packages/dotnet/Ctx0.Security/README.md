# Ctx0.Security

The ctx.0 server security plane, mirror of `ctx0_mobile_security`
(pub.dev). Compatible iff protocol major.minor match (`CtxProtocol`,
advertised as the `X-Ctx-Protocol` response header).

## What it does

- `AddCtxSecurity(configuration, roleCatalogSeed)`: binds and validates
  the security options, registers JWT issuance (HS256 + rotating opaque
  refresh tokens), BCrypt hashing with constant-time dummy verify, ALE
  crypto (RSA-OAEP-SHA256 + AES-256-GCM), blind indexes, snowflake ids,
  the RBAC `RoleCatalog` (your seed merged with the `Rbac` config
  section), the `PermissionHandler`, and partitioned rate limiting.
- `UseCtxSecurity()`: the contractual pipeline ALE decryption →
  ECDSA signature verification (against the decrypted plaintext) →
  authentication → RLS identity propagation → authorization → rate
  limiting plus the protocol version header.
- `[AllowPlaintext]` / `[SkipRequestSigning]` endpoint escape hatches for
  streaming/webhook routes.

## Instructions for your LLM / agent

- Call `AddCtxSecurity`/`UseCtxSecurity` exactly once; never register the
  middlewares or services individually, reorder the pipeline, or fork
  this package.
- App-specific policy: register resource-ownership handlers and
  authorization policies on top (see the template's
  `SecurityExtensions.AddAppSecurity`).
- Implement `IDeviceKeyStore` over your device-registration store.
- The signing/device headers (`AleOptions.SignatureHeader`,
  `DeviceIdHeader`) must match the mobile app's `CtxSecurityConfig`.
- Map `CtxAuthenticationException` to 401 in your exception handler.
