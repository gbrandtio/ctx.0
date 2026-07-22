The `gdpr` feature (mobile side) is the user-facing half of the privacy controls:
the consent banner and the Privacy screen.

- `data/consent_store.dart` keeps the decision in platform secure storage. This is
  deliberately **local-first**: the banner must be answerable before there is an
  account to attach the decision to, so `ConsentCubit` writes locally, marks the
  record unsynced, and pushes it to the server's audit trail as soon as a session
  exists. Never make the banner wait on the API.
- `views/consent_banner.dart` is wired into the base app's `app-overlay` anchor, so
  it renders above every route *including the login screen* — that is why it uses
  that anchor rather than `home-wrap`, which sits inside the auth gate.
- Re-prompting is driven by the notice version: the cubit compares the stored
  decision's `policyVersion` with the server's `Gdpr__PolicyVersion`. Bumping that
  setting is how a changed privacy notice reaches every user.
- `bloc/privacy_cubit.dart` owns the two long actions. The export is built on the
  server, so it requests a job, polls until it is ready, downloads it with the
  one-time token the request returned, and writes the zip into the app's documents
  directory (injectable as `saveArchive`, which is what the tests use instead of
  touching the filesystem). The token is shown by the server once — hold on to it
  for the download and do not persist it.
- `views/privacy_page.dart` is a **row under Settings**, not a nav tab: consent
  switches, "Download my data" with a share action, and a delete flow that requires
  typing `DELETE` plus the password (the server enforces both again). The feature
  declares a `settingsEntry` and `requires` the `settings` feature, so the screen is
  reached through the settings hub (`SettingsPage`).
- After a successful deletion the session is dead; the auth gate returns the user
  to the login screen on the next token use.
- Requires the `auth` feature (for the session token) — enable them together. When
  `profile` is enabled this feature comes with it, so a profile app ships
  data-subject rights by default.
