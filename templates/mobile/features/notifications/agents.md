The `notifications` feature (mobile side) lists per-user notifications with an
unread badge and mark-read, and registers the device for push.

- `data/notifications_repository.dart` calls the JWT-protected `/v1/notifications`
  endpoints, attaching the auth feature's access token via `SecureTokenStore`. It
  uses plain authenticated JSON, **not** the ALE `secureSend` client, because the
  server scopes rows per user by RLS and needs the authenticated identity.
- `bloc/notifications_cubit.dart` holds all I/O behind an immutable
  `NotificationsState`; `init()` registers for push (best effort) then loads.
- `data/push_service.dart` uses `firebase_messaging` to fetch the FCM token and
  register it. It is best-effort: with no Firebase config it silently no-ops, so
  the app runs without push. The firebase deps are added to `pubspec.yaml` via the
  `pubspec-deps` anchor; platform config files (`google-services.json` /
  `GoogleService-Info.plist`) are a documented setup step.
- Requires the `auth` feature (for the session token) — enable them together.
