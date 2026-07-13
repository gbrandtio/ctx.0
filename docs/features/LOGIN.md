# Feature Spec — Login

The login flow is shipped by the mobile template's auth module (email/password + Google Sign-In, backed by [`AUTHENTICATION.md`](AUTHENTICATION.md)). Fill in the product slots.

## Shipped Behavior

- Email/password form with validation, loading discipline (button disabled while submitting), and standardized error mapping (wrong credentials, unverified email, rate-limited).
- Google Sign-In button per Google branding (asset: `templates/mobile/docs/brand-kit/google_logo.svg`).
- Unverified accounts are routed to the verification screen with a resend option.

## Product Slots (fill in)

- **Entry point**: <first screen for signed-out users? reachable from where?>
- **Branding**: <logo/illustration from `templates/mobile/docs/brand-kit/`, copy/tone>
- **Extras**: <e.g., "forgot password" flow enabled? biometric quick-login? — or "defaults">
- **Post-login destination**: <route, must match `AUTHENTICATION.md`>
