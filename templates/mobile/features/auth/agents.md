**Mobile (`app/lib/features/auth/`).** Email/password login and session gating.

- `AuthCubit` holds the session state; `AuthGate` chooses login vs. app shell
  from that state. Views stay pure functions of the Cubit state.
- Access + refresh tokens live in `token_store.dart` on `flutter_secure_storage`
  — never in `SharedPreferences`, never in memory-only.
- `AuthRepository` performs login/refresh over the secure client; a 401 triggers
  a single silent refresh + retry, then sign-out on failure. Do not add ad-hoc
  token handling elsewhere — route all of it through the repository.
