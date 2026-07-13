# Application-Layer Security

This document describes the defense-in-depth measures implemented at the application layer to protect data integrity and confidentiality beyond the TLS transport layer.

---

## 1. Application-Layer Encryption (ALE)

ALE provides an additional layer of confidentiality by encrypting request and response bodies before they are transmitted. This protects data even if TLS is terminated at a proxy or if the network is partially compromised.

*   **Algorithm:** AES-256-GCM (provides both confidentiality and integrity).
*   **Key Exchange (Hybrid Asymmetric):** To prevent secret exposure via app decompilation, ALE uses RSA key wrapping.
    *   **Server:** Holds a private RSA-2048 key.
    *   **Mobile App:** Generates a unique 256-bit AES **Session Key** for every request.
    *   **Key Wrapping:** The app encrypts the Session Key using the Server's Public RSA Key (RSA-OAEP-SHA256) and transmits it in the `X-ALE-Session-Key` header.
*   **Enforcement:** 
    *   **Production/Staging:** Strictly enforced for all API endpoints. Requests missing the `X-ALE-Enabled` or `X-ALE-Session-Key` headers are rejected.
    *   **Development:** ALE is optional if the headers are missing.
*   **Implementation:**
    *   **Request:** The `AleMiddleware` extracts the wrapped session key, unwraps it using the server's private key, and uses it to decrypt the Base64 request body.
    *   **Response:** If ALE is enabled, the middleware uses the same session key to encrypt the response body.
*   **Key Management:** RSA keys are injected via `Security:Ale:RsaPrivateKey` and `Security:Ale:RsaPublicKey` configuration values (environment variables or a secret manager).
    *   **Robustness:** The server supports both raw PEM content and file paths. It automatically handles literal `\n` characters commonly found in environment variables.
    *   **Exposure:** Public keys are exposed to the app as raw PEM content via the `/v1/security/metadata` endpoint.

---

## 2. Request Signing

To prevent request tampering and replay attacks, all requests are cryptographically signed by the mobile application. The system uses a per-device asymmetric signing mechanism (ECDSA).

### Technical Specifications
*   **Algorithm:** ECDSA P-256 (SHA-256).
*   **Key Storage:** The Private Key is generated and stored in the device's hardware security module (Secure Enclave on iOS, KeyStore on Android) and never leaves the device.
*   **Headers:** 
    *   `X-App-Device-Id`: The unique ID of the app installation. (Required)
    *   `X-App-Signature`: `<timestamp>:<signature>`.
*   **Replay Protection:** The timestamp must be within the configured signature window (default: 300 seconds) of server time; requests outside the window are rejected.
*   **Enforcement:** Strictly enforced for all API endpoints in Production/Staging. 
    *   **Bypass:** Registration and metadata endpoints bypass signing via `[SkipRequestSigning]` or `[AllowPlaintext]`. As a fail-safe, the `RequestSigningMiddleware` also employs path-based bypassing for `/v1/security/app-instances` and `/v1/security/metadata`.

---

### Client-Server Interaction Flow

The interaction is divided into two phases: **Bootstrap** (Registration) and **Transactional** (Per-Request Signing).

#### Phase 1: Bootstrap (Key Generation & Registration)
This phase occurs once per app installation or when the device identity is lost.

1.  **Device Key Generation:** The mobile app generates an ECDSA P-256 key pair. The private key is stored securely (hardware-backed) and the public key is extracted.
2.  **Registration Request:** The client sends a `POST /v1/security/app-instances` request.
    *   **Body:** Contains the `DeviceId` and the `PublicKey` (Base64 encoded).
    *   **Security:** This request **MUST be encrypted using ALE** (see Section 1). This ensures that only the genuine app (possessing the server's RSA public key) can register a signing key.
3.  **Server Validation:** The API decrypts the ALE payload, extracts the public key, and persists it in the `app_instances` table linked to the `DeviceId`.

#### Phase 2: Transactional (Per-Request Signing)
For every subsequent API request (e.g., fetching profile, submitting payments):

1.  **Client-Side Preparation (Sign Plaintext):**
    *   **Timestamp:** The app generates a current UTC Unix timestamp (seconds).
    *   **Signing String:** The app constructs a canonical string for signing:
        `METHOD|PATH|TIMESTAMP|BODY`
        *   `METHOD`: Uppercase (e.g., `POST`).
        *   `PATH`: Lowercase (e.g., `/v1/payments/intents`).
        *   `TIMESTAMP`: The Unix timestamp generated above.
        *   `BODY`: The **raw, unencrypted JSON plaintext** request body. The string should be **trimmed** of leading/trailing whitespace.
2.  **Client-Side Signing:** The app signs the UTF-8 bytes of the signing string using its ECDSA private key and Base64-encodes the resulting signature.
3.  **Client-Side Encryption (ALE):** **AFTER** generating the signature, the app encrypts the JSON plaintext body using ALE (AES-GCM). The signature protects the inner plaintext data.
4.  **Transmission:** The app transmits the request with the ALE encrypted body, attaching the headers `X-App-Device-Id`, `X-App-Signature: <timestamp>:<signature>`, and `X-ALE-Session-Key`.
5.  **Server-Side Verification (Middleware Order):**
    *   **Decryption First (`AleMiddleware`):** The API first intercepts the request and decrypts the ALE payload back into the original JSON plaintext.
    *   **Signature Verification (`RequestSigningMiddleware`):** The signing middleware then reconstructs the `METHOD|PATH|TIMESTAMP|BODY` string using the newly **decrypted** body and verifies the signature using the stored public key.

---

### Signature & Key Compatibility
To ensure compatibility with diverse mobile platforms (iOS, Android, React Native), the API supports multiple formats:
*   **Signature Format:** Supports both **IEEE P1363** (64-byte raw R|S) and **DER-encoded** (ASN.1) signatures. DER signatures (common on mobile) are automatically converted to P1363 before verification.
*   **Public Key Format:** Supports both **SubjectPublicKeyInfo (X.509)** and **Raw Uncompressed** (65 bytes starting with `0x04`) formats.

### Error Handling
*   **401 "Request signature is required":** The header was missing entirely.
*   **401 "Request signature verification failed":** The signature was present but invalid (tampering or key mismatch).
*   **401 "Device not registered":** The `DeviceId` was not found in the database. The client should re-trigger **Phase 1 (Bootstrap)**.

---

## 3. Security Metadata Endpoint

The API exposes a specialized endpoint to help the mobile app maintain its security posture without requiring hardcoded updates.

*   **Route:** `GET /v1/security/metadata` (Anonymous)
*   **Payload:**
    ```json
    {
      "aleEnabled": true,
      "alePublicKey": "-----BEGIN PUBLIC KEY-----\n...",
      "requestSigningRequired": true,
      "signatureWindowSeconds": 300,
      "supportedAttestationTypes": ["GooglePlayIntegrity", "AppleAppAttest"]
    }
    ```
*   **Purpose:** Allows the app to retrieve the current ALE Public Key, and check current security requirements on startup.

---

## 4. Device Attestation

The API provides infrastructure to verify that requests originate from a genuine, untampered mobile application running on a secure device.

*   **Supported Providers:**
    *   **Google Play Integrity API** (Android)
    *   **Apple App Attest** (iOS)
*   **Verification Logic:** Critical endpoints (e.g., registration, payments) can be configured to require an attestation token, which is verified against the respective cloud providers via the `IDeviceAttestationService`.

---

## 5. Input Sanitization and XSS Prevention

The API employs automated input sanitization to prevent Cross-Site Scripting (XSS) and other injection attacks by ensuring that all incoming string data is cleaned of malicious HTML/JS payloads.

*   **Mechanism:** `SanitizationFilter` (ASP.NET Core `IEndpointFilter`).
*   **Enforcement:** Applied globally to all `/v1` route groups.
*   **Implementation:**
    *   **Automated Reflection:** The filter recursively scans all incoming Request DTOs (depth-limited to 10) for `string` properties.
    *   **Sanitization Engine:** Uses the `HtmlSanitizer` library to strip potentially dangerous tags (`<script>`, `<iframe>`, etc.) and attributes (`onclick`, `onerror`) while preserving safe, plain text.
    *   **Immutable Types Support:** The filter correctly handles C# `record` types by using reflection to update `init` properties during the API pipeline execution phase.
*   **Scope:** This layer ensures that even if client-side validation is bypassed, no executable scripts can be stored in the database or reflected back to other users.
