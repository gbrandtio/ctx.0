**API (`api/src/.../Auth/`).** Credential store + JWT issuance with rotating refresh.

- Passwords are hashed with the vendored PBKDF2 hasher — never store or compare
  plaintext, never swap in a weaker hash.
- Refresh tokens **rotate** on every use and carry **reuse detection**
  (`EfRefreshTokenStore`): presenting a already-rotated token must revoke the
  family. Preserve this invariant in any change to the refresh path.
- Endpoints: `AuthEndpoints.cs` (wired at the `endpoints` anchor). Entity +
  EF config live under `Domain/Auth` and `Persistence/Configurations`; add a
  migration for any schema change.
- Access tokens are short-lived; put authorization on endpoints, not in the client.
