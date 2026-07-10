# Feature Spec — Authentication

Authentication is a **shipped capability**: the behavior below is implemented by both templates and is not product-specific. Fill in only the marked slots.

## Shipped Behavior (do not re-specify)

- **Methods**: email + password (with mandatory email verification) and Google Sign-In.
- **Token model**: short-lived JWT access token + rotating refresh token with reuse detection and family revocation. Protocol source of truth: `api-template/docs/security/AUTHENTICATION.md`; client mirror: `mobile-template/docs/SECURITY.md`.
- **Session lifecycle**: silent refresh, logout revokes the refresh-token session and unregisters the push token; password change revokes all sessions.
- **Transport protection**: all auth calls go through the standard security pipeline (rate-limited `auth` / `account_creation` policies, request signing, ALE).

## Product Slots (fill in)

- **Additional identity providers**: <e.g., Apple Sign-In — or "none">
- **Token lifetimes**: <keep defaults (15 min access / 30 days refresh) or override with rationale>
- **Post-login destination**: <route the user lands on after authentication>
- **Account model notes**: <e.g., single consumer role only, or additional principal types per `api-template/docs/security/AUTHORIZATION.md`>

## Related Specs

- Login screen: [`LOGIN.md`](LOGIN.md) · Signup screen: [`SIGNUP.md`](SIGNUP.md) · Profile & account management: [`USER_PROFILE.md`](USER_PROFILE.md)
