# Environment Variables Reference

This document centralizes all environment variables used in the mobile client project. These variables are categorized into Android build-time variables and Flutter app-level variables.

## 1. Android Build-time Variables
These variables are primarily used in `android/app/build.gradle.kts` for signing the release APK and configuring the native build environment. They can be provided via system environment variables or a `key.properties` file in the `android` root.

| Variable | Description | Source | Required for Release |
| :--- | :--- | :--- | :--- |
| `RELEASE_STORE_FILE` | Absolute or relative path to the `.jks` keystore file. | Env / `key.properties` | Yes |
| `RELEASE_STORE_PASSWORD` | Password for the keystore. | Env / `key.properties` | Yes |
| `RELEASE_KEY_ALIAS` | Alias for the signing key. | Env / `key.properties` | Yes |
| `RELEASE_KEY_PASSWORD` | Password for the signing key. | Env / `key.properties` | Yes |
| `MAPS_API_KEY` | Google Maps API Key (also used as a manifest placeholder). Only relevant while the `maps_google` integration is enabled (`docs/INTEGRATIONS.md`). | Env / `--dart-define` | Only with `maps_google` |
| `RASP_ANDROID_PACKAGE_NAME` | Android package name for RASP verification. | Env / `--dart-define` | No (Default: your application ID) |
| `RASP_ANDROID_SIGNING_HASH` | SHA-256 fingerprint(s) of the signing certificate. | Env / `--dart-define` | No (Default: Empty) |

## 2. Flutter App-level Variables
These variables are consumed by the Dart layer using `String.fromEnvironment`. They MUST be provided during the build or run command using the `--dart-define` or `--dart-define-from-file` flags.

| Variable | Description | Format | Class |
| :--- | :--- | :--- | :--- |
| `API_BASE_URL` | Base URL of the backend API (e.g., `https://api.example.com`). | String | `ApiConstants` |
| `MAPS_API_KEY` | Google Maps API Key (`maps_google` integration only). | String | `ApiConstants` |
| `RASP_ANDROID_PACKAGE_NAME` | Android package name used for Talsec verification. | String | `SecurityConstants` |
| `RASP_ANDROID_SIGNING_HASH` | Comma-separated list of SHA-256 fingerprints. | Base64 List | `SecurityConstants` |
| `RASP_IOS_BUNDLE_ID` | iOS Bundle Identifier for Talsec verification. | String | `SecurityConstants` |
| `RASP_IOS_TEAM_ID` | Apple Developer Team ID for Talsec verification. | String | `SecurityConstants` |
| `RASP_WATCHER_MAIL` | Email receiving Talsec security reports; RASP stays inactive while empty. | String | `SecurityConstants` |
| `APP_NAME` | Display name (window title, PaymentSheet merchant name). | String | `AppConfig` |
| `PRIVACY_POLICY_URL` / `TERMS_OF_SERVICE_URL` | GDPR/legal links shown in Settings (see `APP_SHELL.md` §4; `settings` feature, `docs/INTEGRATIONS.md`). | String | `AppConfig` |
| `STRIPE_PUBLISHABLE_KEY` | Stripe **publishable** key (`payments_stripe` integration only). The secret key exists only on the API. | String | `AppConfig` |
| `APPLE_PAY_MERCHANT_ID` | Apple Pay merchant identifier (`payments_stripe` only; empty disables Apple Pay). | String | `AppConfig` |
| `MERCHANT_COUNTRY_CODE` | ISO country code for Google Pay / Apple Pay (`payments_stripe` only; default `US`). | String | `AppConfig` |
| `USE_MOCK_DATA` | Switch ApiServices to simulated data (default `false`). | bool | `ApiConstants` |

> **Note on request signing**: Request signing uses per-device asymmetric ECDSA P-256 key pairs generated at runtime and stored in hardware-backed storage (see `docs/SECURITY.md`). There is **no shared signing secret** to configure and never add one. A build-time shared HMAC secret can be extracted by decompiling the app and must not be reintroduced.

## Usage Examples

### Passing variables via CLI
```bash
flutter run \
  --dart-define=API_BASE_URL=https://api.example.com \
  --dart-define=RASP_ANDROID_SIGNING_HASH=hash1,hash2
```

### Using a JSON file (Recommended)
Create a `config.json` (do NOT commit this file):
```json
{
  "API_BASE_URL": "https://api.example.com",
  "RASP_ANDROID_SIGNING_HASH": "..."
}
```

Then run:
```bash
flutter run --dart-define-from-file=config.json
```

## Security Warning
NEVER commit secrets or `.env`/`config.json` files containing real production keys to the version control system. Use CI/CD secrets (e.g., GitHub Actions Secrets) for production builds.
