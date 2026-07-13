# Feature Spec — Signup

The signup flow is shipped by the mobile template's auth module (registration + mandatory email verification, backed by [`AUTHENTICATION.md`](AUTHENTICATION.md)). Fill in the product slots.

## Shipped Behavior

- Registration via email + password with client- and server-side validation (server is source of truth).
- Email verification is required before first login; the verification screen supports code entry and resend (rate-limited).
- Registration endpoints run under the stricter `account_creation` rate-limit policy and device registration (`/v1/security/app-instances`) happens transparently on first run.

## Product Slots (fill in)

- **Collected fields**: <beyond email/password — e.g., display name, phone; mark PII (encrypted at rest per `templates/api/docs/security/ENVELOPE_ENCRYPTION_ARCHITECTURE.md`)>
- **Consents**: <terms/privacy checkboxes, marketing opt-in — wire into the GDPR surface, see `templates/mobile/docs/APP_SHELL.md`>
- **Onboarding**: <post-verification onboarding steps, or "none">
