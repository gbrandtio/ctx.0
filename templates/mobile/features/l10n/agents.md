The `l10n` feature (mobile side) is how the app speaks the user's language.

- **The ARB files are generated, not written.** `app/lib/l10n/app_<code>.arb` was
  merged by ctx.0 from the per-feature fragments of the languages picked at create
  time. Editing them is fine — that is where translations live now — but adding a
  *language* means regenerating the workspace, not hand-copying a file.
- `flutter gen-l10n` turns those ARB files into `AppL10n` under
  `app/lib/l10n/gen/`. It runs on every build (`generate: true` in pubspec.yaml),
  so a changed string needs no separate command. `app/l10n.yaml` configures it.
- Read strings with `AppL10n.of(context)` — it is non-null (`nullable-getter:
  false`), because every route sits under the `MaterialApp` that installs the
  delegates. New user-facing text goes in the ARB files, never inline in a widget.
- `l10n/l10n_support.dart` is also generated: it names the supported locales, the
  delegates, and each language's own name for the picker. Do not edit it.
- `bloc/locale_cubit.dart` holds the *override*: `null` state means "follow the
  device", which is the default and what `MaterialApp.locale: null` does. Anything
  else is a supported locale, persisted in secure storage. A stored language that
  a later build no longer ships is discarded on load rather than honoured.
- The cubit also reports the language in force to `ctxSecureClient.acceptLanguage`,
  so the API answers its own messages in the same language as the UI. If you add a
  second API client, give it the same header.
- `views/language_page.dart` is the nav tab: "Use device language" plus one row per
  supported locale, each labelled in its own language so it is legible to someone
  who cannot read the current one.
