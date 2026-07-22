The `profile` feature (mobile side) shows and edits the signed-in user's account
profile: display name, bio, and avatar.

- `data/profile_repository.dart` calls the JWT-protected `/v1/profile` endpoints,
  attaching the auth feature's access token via `SecureTokenStore`. It uses plain
  authenticated JSON, **not** the ALE `secureSend` client, because the server
  scopes the row per user by RLS and needs the authenticated identity.
- `bloc/profile_cubit.dart` holds all I/O behind an immutable `ProfileState`
  (`load` / `save`); the view only renders and hydrates its fields once.
- `views/profile_page.dart` edits display name, bio, and an avatar URL, previewing
  the avatar with a `CircleAvatar`. Its app bar carries a gear that opens the
  `settings` feature's `SettingsPage`, and the form ends with a **Log out** button
  that calls `AuthCubit.logout()` (server-side revoke) then `SessionCubit.signedOut()`,
  handing control back to the auth gate.
- **Avatar via `media`:** this page keeps the avatar as a plain URL so it compiles
  and runs whether or not the `media` feature is enabled. The API already accepts
  an `avatarMediaId`; when you enable `media`, wire its picker/upload into this
  page and send the returned id to store an in-app avatar.
- Requires the `auth` feature (for the session token), plus `settings` (the hub the
  gear opens) and `gdpr` (data-subject rights ship with a profile app) — enabling
  `profile` pulls all three.
