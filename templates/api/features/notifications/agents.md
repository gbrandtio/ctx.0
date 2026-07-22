The `notifications` feature (mobile + api, requires `auth`) delivers per-user
notifications two ways: stored in-app rows and optional device push.

- **Storage** follows the `notes` pattern: `Notification.Title`/`Body` are
  **envelope-encrypted** at rest and isolated by **Postgres RLS** on `UserId`
  (never `WHERE user_id = …`). Device `Token`s are encrypted too, with a
  `TokenBlindIndex` HMAC for dedupe/lookup. RLS policies are registered in
  `AddCtxNotifications` (`Api/Notifications/NotificationsBootstrap.cs`).
- **Push** goes through the `IPushSender` seam (`Application/Notifications`). The
  default `LoggingPushSender` logs instead of sending, so the API runs and tests
  pass offline; `FcmPushSender` delivers via the FCM HTTP v1 API (service-account
  JWT → OAuth2 token, no SDK) and is selected when `NOTIFICATIONS_FCM_PROJECT_ID`
  and `NOTIFICATIONS_FCM_SERVICE_ACCOUNT_JSON` are set.
- **Endpoints** (`Api/Endpoints/NotificationsEndpoints.cs`, all under
  `/v1/notifications`, `RequireAuthorization`): `GET /` (list), `GET /unread-count`,
  `POST /` (create + push fan-out), `POST /{id}/read`, `POST /devices` (upsert by
  blind index), `DELETE /devices`.
- The mobile side calls these with the auth feature's JWT (via `SecureTokenStore`),
  not the ALE `secureSend` client, because RLS needs the authenticated user.
- Add a migration for schema changes; the two entities are registered on the
  `DbContext` via `ApplyConfiguration` in the wiring.
