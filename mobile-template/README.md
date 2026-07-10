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

## Layout

- `lib/app/` — the shell: router, bottom-nav scaffold, and `modules.dart`
  (the single registration point for features; see `docs/APP_SHELL.md`).
- `lib/core/` — config, theme, l10n, `Result`, design-system widgets.
- `lib/data/` — API client with the security interceptor chain
  (cache → auth → signing → ALE), storage services, shared repositories.
- `lib/features/` — self-contained feature modules (auth, profile,
  settings shipped; add yours per `docs/APP_SHELL.md` §1).
