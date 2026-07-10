# Client Specs — <Your Product Name> Mobile App

> **TEMPLATE — fill this in before any feature work.**
> This is the top-level functional spec of the mobile client: which screens exist, what each does, and the primary flows. Agents read this to know what the app *is* before diving into per-feature specs in `docs/features/`.

**<Product> mobile app** is a Flutter application, designed primarily for mobile use. <One-paragraph summary of what the app lets users do, and whether other client apps exist (e.g., a separate merchant app).>

## Application Flows

### 1. Login / Signup
* **Signup**: Registration via email and password. Requires email verification. (Spec: `docs/features/SIGNUP.md`)
* **Login**: Authentication via Google or email/password. (Spec: `docs/features/LOGIN.md`)

### 2. Main Page
The Home page features a bottom navigation bar with the following sections:

* **Home**: <What the user sees first — e.g., key stats, recent activity list.>
* **Notifications**: Shows user-specific notifications.
* **<Section 3, e.g., Map>**: <Description.> (Spec: `docs/features/MAPS.md`)
* **Profile**: Displays the user's personal data and allows editing. (Spec: `docs/features/USER_PROFILE.md`)

### 3. <Primary Action Flow, e.g., QR Scan & Payment>
<Numbered end-to-end flow of the app's core action. Reference `docs/features/QR_PAYMENT.md` if payments apply.>

### 4. User Profile
**Personal Data Displayed:** <list>

**User Actions:**
* **Edit Details**: Users can change their <fields>.
* **Personalisation**: Change application theme (Light or Dark mode) and language.
* **Account Management**: Option to permanently delete the account.

## UI/UX

Brand tokens (colors, fonts) are defined per `docs/UI_UX_GUIDELINES.md`; assets live in `docs/brand-kit/`.

| Token | Value |
|---|---|
| Primary Colour | `#______` |
| Light Mode Background | `#FFFFFF` |
| Dark Mode Background | `#121212` |
