## 0.1.0

- Initial extraction of the ctx.0 mobile security plane from the mobile
  template: RASP (`RaspService`), hardware-backed device identity
  (`DeviceIdentityService`), the fixed request-security interceptor chain
  (`CachingClient` → `AuthRefreshClient` → `SecureDeviceSigningClient` →
  `AleClient`) assembled by `ApiServiceFactory`, ALE metadata bootstrap
  (`SecurityMetadataService`), secure storage (`SecureStorageService`),
  and the Hive response cache (`HiveCacheService`).
- All app-specific values (endpoints, header names, RASP identity) enter
  through `CtxSecurityConfig`.
- Wire-protocol lock: `ctxProtocolVersion` / `ctxProtocolHeader` constants
  matching the API's Ctx0.Security packages (compatible iff major.minor
  match); debug builds assert against the API's X-Ctx-Protocol header, and
  shared golden vectors pin the signing string and ALE payload layout.
