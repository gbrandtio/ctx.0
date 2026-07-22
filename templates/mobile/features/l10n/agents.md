The `l10n` feature is the opt-in language *picker* (mobile only). Everything that
makes the app speak the user's language is always-on and lives elsewhere: the
locale state and mobile plumbing (`LocaleCubit`, the secure locale store, the
`MaterialApp` delegates, `l10n.yaml`, `generate: true`) are in the mandatory
`session` layer, and the API's Accept-Language handling is in the API base
(`AddCtxLocalization`). So an app is fully localized whether or not this feature
is enabled; enabling it only adds the tab that lets the user change the language
from inside the app instead of following the device.

- **The ARB files are generated, not written.** `app/lib/l10n/app_<code>.arb` was
  merged by ctx.0 from the per-feature fragments of the languages picked at create
  time. Editing them is fine — that is where translations live now — but adding a
  *language* means regenerating the workspace, not hand-copying a file.
- `flutter gen-l10n` turns those ARB files into `AppL10n` under
  `app/lib/l10n/gen/`. It runs on every build (`generate: true`, configured by
  `app/l10n.yaml` — both shipped by the session layer), so a changed string needs
  no separate command.
- Read strings with `AppL10n.of(context)` — it is non-null (`nullable-getter:
  false`), because every route sits under the `MaterialApp` that installs the
  delegates. New user-facing text goes in the ARB files, never inline in a widget.
- `l10n/l10n_support.dart` is also generated: it names the supported locales, the
  delegates, and each language's own name for the picker. Do not edit it.
- `views/language_page.dart` is the nav tab: "Use device language" plus one row per
  supported locale, each labelled in its own language so it is legible to someone
  who cannot read the current one. It reads and drives the session's `LocaleCubit`
  — selecting a row overrides the device language, which the session persists and
  reports to `ctxSecureClient.acceptLanguage` so the API answers in the same
  language as the UI.
