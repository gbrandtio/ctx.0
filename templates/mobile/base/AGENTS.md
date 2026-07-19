# AGENTS.md — CtxApp (mobile)

Flutter client. **State management is Bloc** (`flutter_bloc`) — every feature is a
Bloc/Cubit with explicit immutable state; views are pure functions of state.

- Composition root: `lib/app/di.dart` (`ctxAppProviders`). Feature overlays register here.
- HTTP goes through the security interceptor chain in `lib/security/` (ALE → signing → JWT). Never call `http` directly for API traffic.
- Secure values via `flutter_secure_storage`; never `SharedPreferences` for secrets.
- Secrets/config come from `--dart-define`, never hardcoded.

Leave `// ctx:anchor:*` markers intact — the ctx0 CLI edits them.
