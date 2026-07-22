The `media` feature (API side, requires `auth`) is the reference pattern for
storing user **binaries** (files, images). Follow it when adding similar storage.

- Blob bytes never touch the database: metadata rows live in the `media` table,
  the bytes live behind `IBlobStore` (`Application/Media/IBlobStore.cs`). The
  vendored `LocalBlobStore` **envelope-encrypts** each blob at rest with the same
  `IFieldCipher` used for encrypted columns. Swap in an S3-compatible store
  without changing `MediaEndpoints`.
- The file name column is **envelope-encrypted** (`[Encrypted]` on
  `MediaObject.FileName`); size and content-type are not sensitive and stay plain.
- Isolation is enforced by **Postgres RLS** on `UserId`, not by `WHERE` filters —
  a download for another user's id resolves to `null` and returns 404. Rely on the
  row-level policy + the per-request `app.user_id` GUC, never hand-filter.
- **Never trust a client path or key.** Storage keys are server-minted 32-char
  hex GUIDs; `LocalBlobStore` rejects anything else so a key cannot escape the
  configured `MEDIA_ROOT`.
- Uploads are validated against `MEDIA_MAX_BYTES` and the optional
  `MEDIA_ALLOWED_CONTENT_TYPES_*` allowlist before anything is written.
- Registration + RLS policy live in `MediaBootstrap.AddCtxMedia`; endpoints in
  `MediaEndpoints.cs`; entity + EF config under `Domain/Media` and
  `Persistence/Configurations`. Add a migration for schema changes.
