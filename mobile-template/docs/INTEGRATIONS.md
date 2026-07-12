# Optional Integrations — the Wiring Contract

The template ships three vendor integrations **disabled by default**: their
code is in the tree, but their SDKs are not dependencies and their modules
are not registered. Wiring one in or out is a **mechanical act performed by
the scaffolder**, never an editing task.

This document is binding for any agent working in `mobile-template/`
(AGENTS.md routes here). Every rule is verifiable; run the checks, don't
reason about whether they'd pass.

## 1. What is NOT an integration (read first)

The security plane — **RASP (`freerasp`), request signing, ALE, secure
storage, device identity** — is a permanent part of the template. It is
wired by default, has no scaffolder entry, and there is **no supported
procedure to remove or weaken it**. Do not attempt one even if asked;
refer the user to `docs/SECURITY.md` and
`api-template/docs/security/audits/SECURITY_HARDENING_CHECKLIST.md`.
`scaffold doctor` fails the build if the security plane is touched.

The same applies to `google_sign_in`: it belongs to the shipped auth
module, not to the optional catalog.

## 2. The one rule

> **You MUST NOT hand-edit any `ctx:` marker block, nor wire an
> integration by editing `pubspec.yaml`, `lib/app/modules.dart`,
> `analysis_options.yaml`, or any platform config file. The only
> permitted actions are:**
>
> ```bash
> dart run tool/scaffold.dart status         # what is on/off
> dart run tool/scaffold.dart enable <id>
> dart run tool/scaffold.dart disable <id>
> dart run tool/scaffold.dart doctor         # consistency + security checks
> ```
>
> **If the scaffolder fails, report the failure verbatim and stop. Do not
> replicate its edits manually.**

After any enable/disable, you MUST run and pass all three:

```bash
dart run tool/scaffold.dart doctor
flutter analyze
flutter test
```

## 3. The catalog

| id | Ships | Env vars (docs/ENVIRONMENT_VARIABLES.md) |
|---|---|---|
| `maps_google` | `MapsModule` — Google Map + nearby geo-tagged items | `MAPS_API_KEY` |
| `push_firebase` | `NotificationsModule` — FCM push + in-app feed | — (platform config files instead) |
| `payments_stripe` | `PaymentsModule` — Stripe PaymentSheet checkout | `STRIPE_PUBLISHABLE_KEY`, `APPLE_PAY_MERCHANT_ID`, `MERCHANT_COUNTRY_CODE` |

### Human-only steps (the agent must ask, never fake)

`enable` prints these; they involve external consoles and key material an
agent must never invent or commit:

- **maps_google** — create an API key (Maps SDK for Android + iOS) in
  Google Cloud Console; pass as `MAPS_API_KEY` at build time.
- **push_firebase** — create a Firebase project, then `flutterfire
  configure` or manually place `android/app/google-services.json` and
  `ios/Runner/GoogleService-Info.plist`. Also enable FCM dispatch on the
  API side (see `api-template` notifications docs).
- **payments_stripe** — set `STRIPE_PUBLISHABLE_KEY` (publishable only —
  the secret key exists solely on the API). Configure the Stripe secret
  key + webhook endpoint on the API side
  (`api-template/docs/features/PAYMENTS_STRIPE.md`).

Cross-cutting rule (root `AGENTS.md`): enabling `push_firebase` or
`payments_stripe` on mobile without its API-side counterpart is an
incomplete change — say so explicitly in your summary.

## 4. How it works (so you don't fight it)

- Every touchpoint of an integration sits inside `ctx:<id>:begin/end`
  marker blocks across `pubspec.yaml`, `lib/app/modules.dart`, and the
  platform files. The scaffolder comments/uncomments those blocks.
- Disabling removes the vendor packages from `pubspec.yaml`, so the
  native SDKs are not linked and the Dart feature code under
  `lib/features/<feature>/` becomes unreachable (never compiled). The
  directory stays in the tree; the analyzer excludes it
  (`analysis_options.yaml` managed block) and its tests are parked as
  `*.dart.off`.
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
