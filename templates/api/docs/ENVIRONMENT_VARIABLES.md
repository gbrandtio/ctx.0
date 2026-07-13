# Environment Variables Reference

This document centralizes the environment variables and secrets required to run the API template. In .NET, configuration values can be provided via `appsettings.json`, environment variables, or secret managers (like Azure Key Vault or AWS Secrets Manager).

When using environment variables, .NET maps hierarchical `appsettings.json` keys by replacing the colon (`:`) with a double underscore (`__`).

## 1. Database Configuration

| Environment Variable | AppSettings Key | Description | Required |
| :--- | :--- | :--- | :--- |
| `ConnectionStrings__Default` | `ConnectionStrings:Default` | The PostgreSQL connection string (e.g., `Host=localhost;Database=app;Username=postgres;Password=...`). | **Yes** |

## 2. Authentication (JWT)

| Environment Variable | AppSettings Key | Description | Required |
| :--- | :--- | :--- | :--- |
| `Jwt__SigningKey` | `Jwt:SigningKey` | A Base64-encoded cryptographic key used to sign JWTs. Must be at least 256 bits (32 bytes) for HMAC-SHA256. | **Yes** |
| `Jwt__Issuer` | `Jwt:Issuer` | The issuer of the token. Default: `app-api`. | No |
| `Jwt__Audience` | `Jwt:Audience` | The intended audience. Default: `app-mobile-client`. | No |

## 3. Security: Envelope Encryption (Data at Rest)

These keys are used for encrypting Personally Identifiable Information (PII) at rest in the database. See `docs/security/ENVELOPE_ENCRYPTION_ARCHITECTURE.md` for details.

| Environment Variable | AppSettings Key | Description | Required |
| :--- | :--- | :--- | :--- |
| `Security__Encryption__CurrentVersion` | `Security:Encryption:CurrentVersion` | The active key version ID used for new encryptions (e.g., `v1`). | **Yes** |
| `Security__Encryption__Keys__<version>` | `Security:Encryption:Keys:<version>` | The Base64-encoded 256-bit AES key for the specified version (e.g., `Security__Encryption__Keys__v1`). | **Yes** |
| `Security__Encryption__BlindIndexKey` | `Security:Encryption:BlindIndexKey` | A Base64-encoded HMAC key used to generate deterministic search hashes for encrypted columns. | **Yes** |

## 4. Security: Application-Layer Encryption (ALE) & Request Signing

These keys secure the transport layer for high-security endpoints. See `docs/security/APPLICATION_LAYER_SECURITY.md`.

| Environment Variable | AppSettings Key | Description | Required |
| :--- | :--- | :--- | :--- |
| `Security__Ale__RsaPrivateKey` | `Security:Ale:RsaPrivateKey` | Base64-encoded RSA Private Key (PKCS#8). Used to decrypt incoming ALE payloads from clients. | **Yes** |
| `Security__Ale__RsaPublicKey` | `Security:Ale:RsaPublicKey` | Base64-encoded RSA Public Key (X.509). Used to encrypt outgoing ALE responses to clients. | **Yes** |

## 5. Third-Party Integrations

These variables are only required if you have enabled the corresponding feature integrations (via `ctx0 enable`).

| Environment Variable | AppSettings Key | Description | Required |
| :--- | :--- | :--- | :--- |
| `Stripe__SecretKey` | `Stripe:SecretKey` | Your Stripe Secret API Key (starts with `sk_test_` or `sk_live_`). | Only with `payments_stripe` |
| `Stripe__WebhookSecret` | `Stripe:WebhookSecret` | Your Stripe Webhook Endpoint Secret (starts with `whsec_`). | Only with `payments_stripe` |
| `Authentication__Google__ClientIds__0` | `Authentication:Google:ClientIds:0` | Your Google OAuth Client ID for verifying tokens from the mobile app. Add `__1`, `__2` for additional IDs. | Only with `auth_google` |

## 6. Client App Controls

| Environment Variable | AppSettings Key | Description | Required |
| :--- | :--- | :--- | :--- |
| `MINIMUM_CLIENT_VERSION` | N/A | Semantic version (e.g., `1.0.0`). If set, the API rejects requests from older clients with `426 Upgrade Required`. | Only with `app_updates` |

## Usage Examples

### Local Development (appsettings.Development.json)
For local development, the easiest way to provide these is by adding them to `AppApi/appsettings.Development.json` (which is excluded from source control) or using the [.NET Secret Manager (`dotnet user-secrets`)](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets).

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Database=app_dev;Username=postgres;Password=secret"
  },
  "Jwt": {
    "SigningKey": "YOUR_BASE64_ENCODED_JWT_SECRET_HERE="
  }
}
```

### Docker Compose
When running via Docker, you can pass them in the `environment` block or via a `.env` file:
```yaml
services:
  api:
    image: app-api
    environment:
      - ConnectionStrings__Default=Host=db;Database=app;Username=postgres;Password=secret
      - Jwt__SigningKey=YOUR_BASE64_ENCODED_JWT_SECRET_HERE=
```

### Linux / macOS Shell
```bash
export ConnectionStrings__Default="Host=localhost;Database=app;Username=postgres;Password=secret"
export Jwt__SigningKey="YOUR_BASE64_ENCODED_JWT_SECRET_HERE="
dotnet run --project AppApi
```

> **Security Warning:** NEVER commit `appsettings.Development.json` or `.env` files containing real production secrets. Use CI/CD secret managers or cloud key vaults (AWS KMS, Azure Key Vault, Google Secret Manager) for production deployments.
