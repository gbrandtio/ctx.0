The `gdpr` feature (mobile + api, requires `auth`) gives the signed-in user the
three data-subject rights the regulation expects: consent (Art. 7), a copy of
their data (Art. 15/20), and erasure (Art. 17).

- **Coverage is composed, not hard-coded.** Every feature that stores user-owned
  rows registers an `IPersonalDataContributor` (declared in the always-on base,
  `Application/Abstractions/`), and this feature aggregates whatever is registered.
  So an export covers exactly the features enabled in this workspace, and enabling
  another one widens both the export and the erasure with no change here. A feature
  you write yourself is included the moment it registers a contributor — and is
  silently *missing* from both until it does. `media` also implements
  `IPersonalDataAttachments`, which is how the user's files ride along in the archive.
- **Consent** (`consent_records`) is append-only: a withdrawal is a new row with
  fewer purposes, so the table is the audit trail. The newest row is the user's
  current position. The app decides locally first (the banner has to work before
  sign-in) and syncs once there is a session, so treat the server rows as the record
  of what was agreed and the device copy as a cache.
- **Export** is asynchronous. `POST /v1/privacy/export` writes a `data_export_jobs`
  row and queues it; `ExportJobRunner` builds a zip (`export.json` plus attachments)
  and stores it through `ExportArchiveStore`, envelope-encrypted at rest exactly like
  a media blob. The response carries a one-time download token **once** — only its
  hash is stored — and the archive is deleted as it is served. Retention is enforced
  on the caller's own rows when they request a new export, not by a privileged sweep,
  because every query here is RLS-scoped.
- **RLS in the background.** `ICurrentUser` normally comes from the request
  principal, so a queued export would see no rows at all. `AddCtxGdpr` swaps in
  `SubjectScopedCurrentUser`, which honours the subject the runner declares via
  `PersonalDataSubject.Enter(userId)`. Any other background work that needs to read a
  specific user's rows must do the same — otherwise RLS correctly returns nothing.
- **Erasure** is immediate and hard. `AccountEraser` runs every contributor, drops
  the consent trail and export archives, then the `Users` row, in one transaction —
  a failure leaves the account intact rather than half-deleted. Deleting the refresh
  tokens is what ends live sessions; an access token stops working as soon as the
  user row is gone. `POST /v1/privacy/account/delete` re-authenticates with the
  password and requires `confirm: "DELETE"` first.
- **Endpoints** (`Api/Endpoints/PrivacyEndpoints.cs`, all under `/v1/privacy`,
  `RequireAuthorization`): `GET|PUT /consent`, `POST /export`, `GET /export/{id}`,
  `GET /export/{id}/download`, `POST /account/delete`.
- The export queue and archive store are in-process/filesystem, like the media
  feature's blob store: fine for one instance, replace both when you scale out.
- Add a migration for schema changes; both entities are registered on the
  `DbContext` via `ApplyConfiguration` in the wiring.
