The `session` layer is the mandatory application-layer seam every app is built
on. It is not a feature and cannot be turned off: it owns "who is signed in, with
what credentials, in what language" so that no feature has to.

- **`session/token_store.dart` — the credentials.** `ctxSession` is the single
  app-wide `TokenStore`. Every repository that sends `Authorization: Bearer`
  reads its access token through this one instance, which is what keeps refresh
  rotation single-flight. It also exposes `sessionLost`, fired when the API
  rejects a refresh. Do not construct a second store; pass `ctxSession`.
- **`session/session_cubit.dart` — the sign-in status.** `SessionCubit` is the
  single source of truth for `SessionStatus` (`unknown` → `anonymous` /
  `authenticated`). `restore()` runs at start from the stored token, and it
  drops to `anonymous` when `ctxSession` reports the session lost. The auth gate
  and any sign-in-dependent UI read this cubit, never a feature's own flag.
- **`session/locale_cubit.dart` + `session/locale_store.dart` — the locale.**
  `LocaleCubit` holds the locale override handed to `MaterialApp.locale` (`null`
  = follow the device). It reports the language in force to
  `ctxSecureClient.acceptLanguage`, so the API answers in the app's language.
  The MaterialApp delegates, `supportedLocales`, `generate: true` and
  `l10n.yaml` all ship from this layer, so an app is localized even with no
  `l10n` feature enabled.

**The plug-in contract:**

- **Providers feed the session.** A feature that authenticates stores the tokens
  in `ctxSession` and then calls `context.read<SessionCubit>().signedIn()` (or
  `signedOut()`). That is the whole handshake — providers keep no session state.
  The `l10n` feature is the language picker; it drives the `LocaleCubit`.
- **Consumers read the session.** A feature that needs the bearer token imports
  `package:ctxapp/session/token_store.dart` and passes `ctxSession` to its
  repository. A feature that varies by sign-in watches `SessionCubit`.

Both cubits and `ctxSession` are registered app-wide by this layer's wiring in
`app/lib/app/di.dart`, so they are in scope for every route.
