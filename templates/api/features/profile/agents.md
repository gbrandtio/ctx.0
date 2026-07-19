The `profile` feature (API side, requires `auth`) stores the user's editable
account profile as a **satellite of the base `User`** — a one-to-one row keyed by
`UserId`, exactly like the auth feature's `UserCredential`. Follow this pattern
for other per-user singletons instead of adding columns to the base `User`.

- `DisplayName` and `Bio` are **envelope-encrypted** at rest (`[Encrypted]`);
  non-sensitive avatar fields stay plain.
- Isolation is enforced by **Postgres RLS** on `UserId` (policy registered via the
  `services` wiring). `GET /v1/profile` auto-creates an empty profile on first
  read; `PUT` upserts. Rely on the row-level policy + the per-request
  `app.user_id` GUC, not on `WHERE` filters.
- **Avatar composition with `media`:** `AvatarMediaId` points at a `media` object
  when that feature is enabled; otherwise `AvatarUrl` holds a plain URL. There is
  deliberately no FK, so `profile` stays independently toggleable — enabling
  `media` + `profile` together unlocks avatar upload.
- Endpoints: `ProfileEndpoints.cs`; entity + EF config under `Domain/Profile` and
  `Persistence/Configurations`. Add a migration for schema changes.
