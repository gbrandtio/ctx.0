The `ping` feature is the end-to-end proof of the wire protocol: a signed +
ALE-encrypted round trip with no auth required.

- Mobile: `app/lib/features/ping/` — `PingCubit` calls `PingRepository`, which
  goes through the security `SecureHttpClient`. Keep the request on that client;
  never bypass the interceptor chain.
- API: `api/src/Api/Endpoints/PingEndpoints.cs` — `MapPingEndpoints()` (wired at
  the `endpoints` anchor in `Program.cs`). It echoes the decrypted body back
  through ALE, so it exercises signature verify + decrypt + encrypt.
- If a round-trip test fails, suspect the shared vectors first (`.ctx/vectors.json`),
  not the endpoint.
