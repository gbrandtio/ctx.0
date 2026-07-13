# Mobile Template (Flutter)

Production app shell + plug-n-play feature modules. Agents and developers:
**start at `AGENTS.md`** — it routes every task to the binding docs in
`docs/`.

## Quick start

```bash
flutter pub get
flutter run --dart-define-from-file=config.json   # see docs/ENVIRONMENT_VARIABLES.md
flutter test
```

## Scaffoldable features

Every shipped feature except the security plane and the auth core is
toggleable via the scaffolder (`docs/INTEGRATIONS.md`): vendor
integrations (Google Maps, Firebase push, Stripe checkout) ship disabled —
their SDKs are not compiled until you opt in — while the profile and
settings tabs and the auth module's sign-in methods (`auth_google`,
`auth_email_password`; at least one must stay enabled) ship enabled. Wire
them **only** via the scaffolder:

```bash
dart run tool/scaffold.dart status
dart run tool/scaffold.dart enable maps_google      # push_firebase | payments_stripe | ...
dart run tool/scaffold.dart disable auth_google     # email/password-only auth
dart run tool/scaffold.dart doctor
```

Security (RASP, request signing, ALE) and the auth core (AuthBloc, token
lifecycle, logout) are permanent and not part of this catalog — see
`docs/SECURITY.md`.

## Layout

- `lib/app/` — the shell: router, bottom-nav scaffold, and `modules.dart`
  (the single registration point for features; see `docs/APP_SHELL.md`).
- `lib/core/` — config, theme, l10n, `Result`, design-system widgets.
- `lib/data/` — API client with the security interceptor chain
  (cache → auth → signing → ALE), storage services, shared repositories.
- `lib/features/` — self-contained feature modules (auth, profile,
  settings shipped; add yours per `docs/APP_SHELL.md` §1).
