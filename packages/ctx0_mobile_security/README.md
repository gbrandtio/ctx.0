# ctx0_mobile_security

The **permanent mobile security plane** of the ctx.0 platform. Ships as a
compiled dependency of every generated app: consumers pin or update the
version — they never edit, vendor, or override this code.

## What it does

| Component | Responsibility |
|---|---|
| `RaspService` | Runtime Application Self Protection (Talsec/freerasp): root/jailbreak, debugger, emulator, tamper, hook detection. Reaction policy: force close. Release builds only. |
| `DeviceIdentityService` | Per-device ECDSA P-256 key pair in secure storage; first-run registration; request-signing key. |
| `ApiServiceFactory` | Assembles the contractual interceptor chain: `CachingClient → AuthRefreshClient → SecureDeviceSigningClient → AleClient → network`. The order mirrors the server pipeline (sign the plaintext, encrypt after) and cannot be changed. |
| `SecureDeviceSigningClient` | ECDSA signature over `METHOD\|PATH\|TIMESTAMP\|BODY`; self-healing device registration on 401 "Device not registered." |
| `AleClient` | Application-Layer Encryption: AES-256-GCM body encryption with RSA-OAEP-wrapped session keys; decrypts 2xx responses. |
| `AuthRefreshClient` | Bearer attach + single-flight rotating refresh-token flow; fires `onSessionExpired` when the session is gone. |
| `CachingClient` / `HiveCacheService` | Response cache with bypass header; a hit never touches the network. |
| `SecureStorageService` | Platform keystore-backed storage for tokens and device keys. |
| `SecurityMetadataService` | Unencrypted ALE bootstrap: fetches the server's RSA public key. |
| `CryptoUtils` | ECDSA/RSA/AES-GCM primitives with zero-memory hygiene. |

## How your app uses it

All app-specific values enter through **`CtxSecurityConfig`**, built in one
place — the generated `lib/app/security_bootstrap.dart`:

```dart
final config = buildSecurityConfig();     // endpoints, headers, RASP identity
await RaspService(config.rasp).init();    // FIRST, before any secret is read
final apiFactory = ApiServiceFactory(
  config: config,
  secureStorage: secureStorage,
  deviceIdentity: deviceIdentity,
  cacheService: cacheService,
  onSessionExpired: () => authRepository.onSessionExpired(),
);
// every ApiService uses apiFactory.client
```

## Instructions for your LLM / agent

- **Never** reimplement, fork, or vendor anything in this package; never add
  a `dependency_overrides` entry for it. The template's `doctor` command
  fails the build if you do.
- To change endpoints, header names, or RASP identity, edit
  `lib/app/security_bootstrap.dart` — the only sanctioned seam.
- The signing header (`X-App-Signature` by default) must match the API's
  `RequestSigningMiddleware` configuration; the package version's
  major.minor must match the API's `Ctx0.Security` packages (shared wire
  protocol).
- Sequencing is contractual: RASP init before storage reads; the interceptor
  chain order is fixed inside `ApiServiceFactory`.
- Extension point: pass a custom `networkClient` only for tests.
