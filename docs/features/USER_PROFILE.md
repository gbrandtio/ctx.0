# Feature Spec — User Profile

The profile feature is shipped by the mobile template's profile module plus the app shell's settings/GDPR surface (`templates/mobile/docs/APP_SHELL.md`). Fill in the product slots.

## Shipped Behavior

- Profile screen showing the user's personal data; edit flow with optimistic UI and server-side validation.
- **Personalisation**: theme (light/dark) and language, persisted per user and reset on logout.
- **Password change**: dedicated flow requiring the old password; revokes all sessions (see `templates/api/docs/security/audits/SECURITY_HARDENING_CHECKLIST.md` §9).
- **GDPR account management**: permanent account deletion (server-side anonymization) and data export, surfaced in settings.
- PII fields are envelope-encrypted at rest; searchable fields use blind indexes.

## Product Slots (fill in)

- **Displayed fields**: <list>
- **Editable fields**: <list — anything sensitive needs the PII treatment above>
- **Avatar/media**: <supported? storage location? — or "none">
- **Product-specific sections**: <e.g., preferences, linked accounts — spec each as its own feature if non-trivial>
