**Mobile (`app/lib/features/auth/`).** Email/password login: a provider that
plugs into the mandatory `session` layer.

- Auth owns the *login form*, not the app's sign-in status. `AuthCubit` performs
  the credential exchange through `AuthRepository` and reports the outcome
  (`submitting` → `success` / `failure`); it keeps no session state. On
  `success` the login view calls `context.read<SessionCubit>().signedIn()`, and
  the app-wide `SessionCubit` (in `session/`) is what actually flips the app from
  the login screen to the shell.
- `AuthGate` renders from `SessionCubit`, never from `AuthCubit`: `authenticated`
  shows the app, `anonymous` shows `LoginPage`, `unknown` shows a spinner while
  the session restores.
- Tokens are not stored here. `AuthRepository` writes the access + refresh pair
  into the shared `ctxSession` credential store (`session/token_store.dart`) on
  `flutter_secure_storage`, and `logout()` revokes the family server-side before
  clearing it. Session loss on a rejected refresh is detected by `SessionCubit`,
  not by auth.
- Views stay pure functions of the Cubit state; new user-facing strings go in the
  ARB files, never inline.
