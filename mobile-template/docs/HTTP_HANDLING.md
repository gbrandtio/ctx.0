# HTTP Handling — Client Integration with the API

This document describes the specification for integrating the mobile client with the backend API. For the swagger documentation refer to `docs/API/swagger.json`.

## Specification - HTTP Handling

- Constants such as API Version, URI, http/https, endpoints are defined in `./lib/core/constants/api_constants.dart`. The constants include also a `useMockData` option that allows developers to switch between simulated mode and real API integration.
- The API response models are defined in `./lib/models/`.
- The API requests implementation is implemented in `lib/data/services/api/`.

### Cache Control & Manual Sync
All `GET` requests in the `ApiService` hierarchy support an optional `forceRefresh` parameter (default `false`).
*   **Behavior**: When `true`, the client adds the `X-Bypass-Cache` header.
*   **Usage**: Primarily used for manual UI actions like "Pull-to-Refresh" to ensure the latest data is retrieved from the server, bypassing the local Hive cache.

## Application Layer Encryption (ALE)

The application implements Hybrid ALE for all outgoing requests with a body and incoming responses to ensure data privacy even if TLS is compromised.

- **Architecture**: Hybrid Encryption (RSA + AES-GCM).
- **Bootstrapping**: The client retrieves the server's PEM-encoded RSA-2048 public key from `GET /v1/security/metadata` on startup.
- **Session Key Generation**: A cryptographically secure random 256-bit AES key is generated for each request.
- **Key Wrapping**: The session key is encrypted with the server's RSA public key using RSA-OAEP with SHA-256 (RSA/ECB/OAEPWithSHA-256AndMGF1Padding).
- **Payload Encryption**: The JSON request body is encrypted using AES-256-GCM.
- **Headers**:
    - `X-ALE-Enabled`: `true`.
    - `X-ALE-Session-Key`: Base64 string of the RSA-wrapped AES key.
- **Payload Format**: The final encrypted body is a Base64 string of the concatenated bytes: `Nonce (12) | Tag (16) | Ciphertext (n)`.
- **Implementation**: `./lib/data/services/api/interceptors/ale_client.dart` and `./lib/data/services/api/security_metadata_service.dart`.
- **Behavior**:
    - Decryption is proactively performed on **2xx** responses using the same session key, even if response headers are missing.
    - Error responses (4xx, 5xx) are **NEVER encrypted** to ensure diagnostic messages are always accessible.
    - Session keys are securely cleared from memory (Zero Memory hygiene) once the response is processed.

## Error Response Parsing

All error responses (4xx and 5xx) follow a consistent JSON structure (RFC 9457 Problem Details). For detailed implementation and architectural guidelines, refer to `docs/ERROR_HANDLING.md`.

### Unified Response Structure
```json
{
  "status": 409,
  "title": "Conflict",
  "detail": "Email already exists.",
  "instance": "/v1/users",
  "traceId": "0HN18E6N3C4R1:00000001"
}
```


## Request Signing

To ensure request integrity and authenticity, every request is signed using a per-device ECDSA P-256 signature.

- **Algorithm**: ECDSA P-256 (Asymmetric).
- **Encoding**: ASN.1 DER (Base64 encoded).
- **Implementation**: `./lib/data/services/api/interceptors/secure_device_signing_client.dart`.
- **Key Management**: Hardware-backed keys managed by `DeviceIdentityService`.
- **Signature Payload**: `METHOD|PATH|TIMESTAMP|BODY`
    - `METHOD`: Uppercase HTTP method (e.g., `POST`).
    - `PATH`: Lowercase request path (e.g., `/v1/users/login`).
    - `TIMESTAMP`: Unix timestamp in seconds.
    - `BODY`: The **raw PLAINTEXT** request body.
    - **Note**: Even when ALE is enabled, the signature must be computed over the plaintext JSON, not the ciphertext. The server decrypts the payload before verifying the signature.
- **Headers**:
    - `X-App-Device-Id`: Unique device identifier (UUID v4).
    - `X-App-Signature`: Formatted as `timestamp:signature`.

## Self-Healing Registration

If a request fails with HTTP 401 and the message "Device not registered.", the system automatically:
1. Triggers a registration call to `POST /v1/security/app-instances` (ALE-encrypted).
    - **Payload**: `{ "deviceId": "...", "publicKey": "..." }` (camelCase).
    - **Headers**: Must include `X-App-Device-Id`.
2. Retries the original request once registration is confirmed.

## Interceptor Orchestration

The HTTP client uses a "Chain of Responsibility" pattern implemented in `ApiServiceFactory`. The order of execution is critical for security:

1. **Caching (`CachingClient`)**: Checks for cached responses before proceeding.
2. **Signing (`SecureDeviceSigningClient`)**: Signs the **plaintext** request body.
3. **Encryption (`AleClient`)**: Encrypts the request body after signing.
4. **Network**: Sends the processed request over the wire.

This order matches the server's pipeline, which decrypts the ALE payload first and then verifies the signature against the recovered plaintext. The signature therefore protects the true payload content (integrity), while ALE protects its confidentiality. See the "Sign the Plaintext, Encrypt After" requirement in `docs/SECURITY.md` §4.2.
