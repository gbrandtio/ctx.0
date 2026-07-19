The `notes` feature (API-only, requires `auth`) is the reference pattern for
storing sensitive per-user data. Follow it when adding similar features.

- Title/body are **envelope-encrypted** at rest via the vendored EF value
  converters — the columns hold ciphertext, never plaintext.
- Search uses a **blind index** (HMAC), not the ciphertext or a plaintext column;
  add a blind-index column for any field you must query by.
- Isolation is enforced by **Postgres RLS**, not by `WHERE user_id = …` in
  queries. Rely on the row-level policy + the per-request `app.user_id` GUC set
  by the RLS interceptor; do not hand-filter and assume it is safe.
- Endpoints: `NotesEndpoints.cs`; entity + EF config under `Domain/Notes` and
  `Persistence/Configurations`. Add a migration for schema changes.
