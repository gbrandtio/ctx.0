The `settings` feature (mobile only) is the app's settings hub: one screen that
gathers the opt-in controls other features contribute, so they read as one place
rather than a row of unrelated navigation tabs.

- `views/settings_page.dart` is a self-contained `Scaffold` + `AppBar` over a
  `ListView` of rows. It declares **no** `nav`, so it is never a main-navigation
  tab; it opens from the gear in the profile page's app bar (`Navigator.push`).
- The rows are **generated**, not hand-written. A feature appears here by declaring
  a `settingsEntry` (label, icon, page, import) in its `feature.json` — `l10n`
  contributes the language picker, `gdpr` the privacy controls. At create time the
  engine fills this file's `ctx:gen:settings-imports` and `ctx:gen:settings-entries`
  markers with a row per enabled settings-capable feature, in feature order. Adding
  or removing one of those features regenerates the list; editing the rows by hand
  is fine once the workspace exists.
- With no settings-capable feature enabled the page renders a single localised
  empty-state line (`settingsEmpty`); its own title is `settingsTitle`.
- The feature has no dependencies of its own so it stays reachable in any app that
  ships a settings-capable feature. `profile` requires it and provides the entry
  point; enabling `l10n` or `gdpr` pulls it in as well.
