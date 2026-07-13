# Scaffoldable Features — the Wiring Contract

Apart from the security plane and the auth core, **every shipped feature
is scaffoldable**: vendor integrations (maps, push, payments) ship
disabled — their code is in the tree, but their SDKs are not dependencies
and their modules are not registered — while the shipped feature tabs
(profile, settings) and the auth module's sign-in methods (Google,
email/password) ship enabled. Wiring any of them in or out is a
**mechanical act performed by the scaffolder**, never an editing task.

This document is binding for any agent working in `templates/mobile/`
(AGENTS.md routes here). Every rule is verifiable; run the checks, don't
reason about whether they'd pass.

## 1. What is NOT scaffoldable (read first)

The security plane — **RASP, request signing, ALE, secure storage, device
identity** — ships as the compiled `ctx0_mobile_security` package. It is
wired by default, has no scaffolder entry, and there is **no supported
procedure to remove, weaken, vendor, fork, or version-override it**. Do
not attempt one even if asked; refer the user to `docs/SECURITY.md` and
`templates/api/docs/security/audits/SECURITY_HARDENING_CHECKLIST.md`.
`scaffold doctor` fails the build if the dependency is missing, overridden
via `dependency_overrides`, or if a vendored copy of the plane appears
under `lib/`. App-specific security values (endpoints, header names, RASP
identity) are configured only in `lib/app/security_bootstrap.dart`.

The **auth core** — the login shell, global `AuthBloc`, token/session
lifecycle, and the logout settings tile — is likewise permanent. Only the
two sign-in *methods* are scaffoldable (`auth_google`,
`auth_email_password`), and **at least one must stay enabled**: the
scaffolder refuses to disable the last one and `doctor` fails if both are
off, because the router's auth redirect would lock the whole app out.

## 2. The one rule

> **You MUST NOT hand-edit any `ctx:` marker block, nor wire an
> integration by editing `pubspec.yaml`, `lib/app/modules.dart`,
> `analysis_options.yaml`, or any platform config file. The only
> permitted actions are:**
>
> ```bash
> ctx0 status                                 # what is on/off
> ctx0 enable <id>
> ctx0 disable <id>
> ctx0 doctor                                 # consistency + security checks
> ```
>
> The `ctx0` CLI (`dart pub global activate ctx0_cli`) is the supported
> entry point; `dart run tool/scaffold.dart <same-commands>` is the
> zero-dependency fallback that works right after clone, before
> `flutter pub get`. Both run the same engine over the same catalog
> (`.ctx/integrations.json`) — never edit that file by hand either.
>
> **If the scaffolder fails, report the failure verbatim and stop. Do not
> replicate its edits manually.**

After any enable/disable, you MUST run and pass all three:

```bash
ctx0 doctor
flutter analyze
flutter test
```

## 3. The catalog

| id | Feature | Ships | Env vars (docs/ENVIRONMENT_VARIABLES.md) |
|---|---|---|---|
| `auth_google` | Google Sign-In method of the auth module | enabled | — (OAuth client config instead) |
| `auth_email_password` | Email/password login + signup + email verification | enabled | — |
| `profile` | `ProfileModule` — profile tab, view/edit profile | enabled | — |
| `settings` | `SettingsModule` — settings tab: theme, language, privacy/GDPR | enabled | `PRIVACY_POLICY_URL`, `TERMS_OF_SERVICE_URL` |
| `maps_google` | `MapsModule` — Google Map + nearby geo-tagged items | disabled | `MAPS_API_KEY` |
| `push_firebase` | `NotificationsModule` — FCM push + in-app feed | disabled | — (platform config files instead) |
| `payments_stripe` | `PaymentsModule` — Stripe PaymentSheet checkout | disabled | `STRIPE_PUBLISHABLE_KEY`, `APPLE_PAY_MERCHANT_ID`, `MERCHANT_COUNTRY_CODE` |
| `image_capture` | `ImageCaptureModule` — Device camera and gallery image picker | disabled | — |
| `app_updates` | `AppUpdatesModule` — Forced App Updates overlay (intercepts 426 Upgrade Required) | disabled | — |
| `nav_bottom` | Standard BottomNavigationBar | enabled | — |
| `nav_rail` | Side NavigationRail for tablets/desktop | disabled | — |
| `nav_drawer` | NavigationDrawer hamburger menu | disabled | — |
| `nav_none` | No navigation shell, pure routes | disabled | — |
| `nav_bottom_notched` | BottomAppBar with FAB cutout | disabled | — |
| `nav_tabs` | Top TabBar | disabled | — |

The auth methods live *inside* the permanent auth module: their toggles
comment marker blocks in the shared login files and exclude
`lib/features/auth/google/` or `lib/features/auth/email_password/`. The
GDPR surface (`docs/APP_SHELL.md` §4) ships with `settings`; a product
that disables that tab must re-home delete-account and data-export
before release.

**Navigation variants**: The `nav_*` methods (`nav_bottom`, `nav_rail`, `nav_drawer`, `nav_none`, `nav_bottom_notched`, `nav_tabs`) control the main app shell's routing UI. They are **mutually exclusive**; enabling one automatically disables the currently active one. `nav_bottom` ships enabled by default. Exactly one must be active at any given time.

### Human-only steps (the agent must ask, never fake)

`enable` prints these; they involve external consoles and key material an
agent must never invent or commit:

- **auth_google** — configure the OAuth clients: iOS `GIDClientID` +
  reversed-client-ID URL scheme in `ios/Runner/Info.plist`; Android
  SHA-1 registration in Google Cloud Console. The API-side endpoint
  (`POST /v1/users/google/authenticate`) ships with the templates/api.
- **auth_email_password** — configure the API-side email sender for
  verification codes (send-code → register flow,
  `templates/api/docs/security/AUTHENTICATION.md`).
- **maps_google** — create an API key (Maps SDK for Android + iOS) in
  Google Cloud Console; pass as `MAPS_API_KEY` at build time.
- **push_firebase** — create a Firebase project, then `flutterfire
  configure` or manually place `android/app/google-services.json` and
  `ios/Runner/GoogleService-Info.plist`. Also enable FCM dispatch on the
  API side (see `templates/api` notifications docs).
- **payments_stripe** — set `STRIPE_PUBLISHABLE_KEY` (publishable only —
  the secret key exists solely on the API). Configure the Stripe secret
  key + webhook endpoint on the API side
  (`templates/api/docs/features/PAYMENTS_STRIPE.md`).
- **image_capture** — Ensure `NSCameraUsageDescription` and
  `NSPhotoLibraryUsageDescription` are set appropriately in `Info.plist`
  for production use.
- **app_updates** — Update the App Store numeric ID (`YOUR_APP_ID`) in
  `AppUpdatesOverlay` (`lib/features/app_updates/views/app_updates_overlay.dart`)
  for iOS releases. The Android Play Store package name is handled automatically
  by the scaffolder. Also ensure the `MINIMUM_CLIENT_VERSION` environment
  variable is set on the API to trigger the forced update.

Cross-cutting rule (root `AGENTS.md`): enabling `push_firebase` or
`payments_stripe` on mobile without its API-side counterpart is an
incomplete change — say so explicitly in your summary. Disabling an auth
method is mobile-only: the API keeps both endpoints, which is harmless.

## 4. How it works (so you don't fight it)

- Every touchpoint of a feature sits inside `ctx:<id>:begin/end` marker
  blocks — across `pubspec.yaml`, `lib/app/modules.dart`, and the
  platform files for module-level features; across the shared auth files
  (`auth_module.dart`, `login_bloc.dart`, `login_screen.dart`, ...) for
  the auth methods. The scaffolder comments/uncomments those blocks.
- Disabling removes any vendor packages from `pubspec.yaml`, so the
  native SDKs are not linked and the feature's Dart source dirs become
  unreachable (never compiled). The directories stay in the tree; the
  analyzer excludes them (`analysis_options.yaml` managed block) and
  their tests are parked as `*.dart.off`.
- `doctor` warns (without failing) when no enabled feature contributes a
  bottom-nav tab — the shell then boots to the splash route until a
  product module provides one (`docs/APP_SHELL.md` §5).
- `main.dart` is vendor-free: each module bootstraps its own SDK in
  `FeatureModule.init()` (see `docs/APP_SHELL.md` §1), so toggling an
  integration never touches the bootstrap.

Consequences you must respect:

- Code inside a disabled integration is **not analyzed** — do not accept
  tasks to modify it while disabled; enable it first, edit, verify, and
  restore the previous state if the user wants it off.
- Business features must not import from a disabled integration's
  directory. `flutter analyze` catches this; treat it as a design error,
  not something to suppress.

## 5. Adding a new integration to the catalog (template maintenance)

Only when the user explicitly asks to extend the catalog itself:

1. Build the feature as a standard `FeatureModule` under
   `lib/features/<feature>/`; put any SDK bootstrap in `init()` (must
   degrade gracefully when platform config is missing — the template runs
   before vendors are configured).
2. Wrap every touchpoint in `ctx:<id>:begin/end` markers. Keep prose
   comments **outside** XML marker blocks (the disable transform wraps the
   block in an XML comment, which cannot nest).
3. Describe the integration in the manifest at the top of
   `tool/scaffold.dart` (`markedFiles`, `sourceDirs`, `testDirs`,
   `envVars`, `userSteps`).
4. Add its env vars to `docs/ENVIRONMENT_VARIABLES.md` and a row to the
   catalog table above.
5. Verify the full round-trip: `disable` → doctor/analyze/test →
   `enable` → doctor/analyze/test → restore the shipped state
   (disabled) → `git diff` must show only your intended changes.
