# Security Documentation

This document outlines the security protocols, standards, and implementations for the mobile client application.

## 1. API & Network Security
- **HTTPS Only**: All network communications must use HTTPS to ensure data is encrypted in transit.
- **Application Layer Encryption (ALE)**:
    - **Architecture**: Hybrid Encryption (RSA-2048 + AES-256-GCM).
    - **Bootstrapping**: On app startup, the client retrieves the server's RSA-2048 public key from `GET /v1/security/metadata`.
    - **Session Keys**: A fresh 256-bit AES session key is generated for every request.
    - The session key is valid for a request/response pair and is used to encrypt the request and decrypt the response.
    - **Key Wrapping**: The session key is wrapped using the server's RSA public key with RSA-OAEP (SHA-256).
    - **Payload Encryption**: Request bodies are encrypted with AES-256-GCM using the session key.
    - **Headers**:
        - `X-ALE-Enabled`: true
        - `X-ALE-Session-Key`: Base64-encoded RSA-wrapped AES key.
    - **Implementation**: `AleClient` interceptor (`ctx0_mobile_security` package (`src/api/interceptors/ale_client.dart`)) and `SecurityMetadataService` (`ctx0_mobile_security` package (`src/api/security_metadata_service.dart`)).
    - **Zero Memory Hygiene**: Session keys are overwritten with zeros immediately after the response is processed.

- **Request Signing**:
    - **Algorithm**: ECDSA P-256 (Asymmetric).
    - **Implementation**: `SecureDeviceSigningClient` interceptor (`ctx0_mobile_security` package (`src/api/interceptors/secure_device_signing_client.dart`)).
    - **Secret Management**: Per-device hardware-backed key pair generated on first run (`DeviceIdentityService`).
    - **Signature Payload**: `METHOD|PATH|TIMESTAMP|BODY` where `BODY` is the **plaintext** JSON (see the critical requirement in Section 4.2).
    - **Self-Healing**: Automatically detects 401 "Device not registered" errors, triggers re-registration via `POST /v1/security/app-instances`, and retries the request.
- **SSL/TLS Pinning**: Consideration for high-security environments to prevent Man-in-the-Middle (MitM) attacks. The `/v1/security/metadata` endpoint can serve current pin hashes to avoid hardcoding them in the binary.

## 2. Data Persistence & Storage
- **Avoid Plaintext Storage**: Never use `shared_preferences` for sensitive data (tokens, passwords). It is only for non-sensitive UI preferences.
- **Secure Storage**: Sensitive data must be stored using secure storage solutions (e.g., `flutter_secure_storage`).
- **Minimize Local Storage**: Store only the absolute minimum amount of sensitive data required.
- **Caching Policy**: Only non-sensitive API responses should be cached (via `CachingClient`). Sensitive user tokens must remain in secure storage.
- **"Zero Memory" Hygiene**:
    - Process secrets (API keys, tokens, passwords) as `Uint8List` rather than standard Dart Strings.
    - Manually overwrite byte arrays with zeros immediately after use to prevent sensitive data from persisting in memory.

## 3. Runtime & Platform Security
- **Code Obfuscation**: Enable Dart code obfuscation during production builds (`--obfuscate` and `--split-debug-info`) to hinder reverse engineering.
- **Biometric Authentication**: Integrate `local_auth` for accessing highly sensitive data or performing critical actions.
- **App Lifecycle Handling**: Replace the app switcher snapshot with a blank screen when the app enters the background to protect sensitive information.
- **Runtime Application Self Protection (RASP)**:
    - **Library**: `freerasp` (Talsec).
    - **Scope**: Detects Root/Jailbreak, Emulators, Debuggers, Tampering, Hooking, and Unverified Installation Sources.
    - **Reaction Policy**: The application is configured to **Force Close** immediately upon detection of any security threat to prevent data exfiltration or reverse engineering.
    - **Implementation**: `RaspService` (`ctx0_mobile_security` package (`src/security/rasp_service.dart`)), initialized at app startup.
- **Dependency Management**: Regularly update the Flutter SDK and third-party packages to incorporate the latest security patches.

## 4. End-to-End Security Flow

The application follows a strictly orchestrated security pipeline to ensure the integrity and confidentiality of data.

### 4.1. Phase 1: Bootstrapping (Initialization)
0. **Configuration Seam**: `buildSecurityConfig()` (`lib/app/security_bootstrap.dart`) assembles the `CtxSecurityConfig` (`ctx0_mobile_security` package (`src/security/ctx_security_config.dart`)) — endpoints, signing header names, and RASP identity. This file is the **only** bridge between app constants (`core/constants/`) and the security plane; the plane itself never imports app code, so it can ship as a compiled package. Never wire app values into security services any other way.
1. **RASP Check**: `RaspService` (constructed with `CtxSecurityConfig.rasp`) starts monitoring the environment. If the device is rooted or a debugger is attached, the app terminates immediately.
2. **Metadata Sync**: The app performs an unencrypted handshake with `GET /v1/security/metadata` to retrieve the latest **RSA-2048 Public Key**.
3. **Device Identity**:
    - Generates or loads per-device ECDSA P-256 keys (`DeviceIdentityService`).
    - **Registration**: If not already registered, the app calls `POST /v1/security/app-instances` to register the device ID and Public Key.
4. **Secure Storage Load**: Authenticated tokens are retrieved from Secure Storage into memory as `Uint8List` for subsequent requests.

### 4.2. Phase 2: Outgoing Request Pipeline (Chain of Responsibility)
Every request triggered by a feature passes through the following interceptors:

> **CRITICAL ARCHITECTURAL REQUIREMENT: Sign the Plaintext, Encrypt After**
> To ensure successful request signing and encryption, the mobile app must follow this exact sequence:
> 1. **Generate JSON Plaintext**: Serialize the request object.
> 2. **Construct Signing String**: Create the canonical string using the **PLAINTEXT** JSON: `METHOD|PATH|TIMESTAMP|PLAINTEXT_JSON_BODY`.
> 3. **Sign the Plaintext**: Sign using the ECDSA private key.
> 4. **Encrypt the Body (ALE)**: Encrypt the same plaintext JSON using AES-GCM.
> 5. **Transmit**: Send the ciphertext while including the signature of the plaintext in the headers.
>
> **Rationale**: The server decrypts the ALE payload *before* it reaches the signing middleware. It then reconstructs the signing string using the decrypted plaintext to verify the signature. Signing the ciphertext will result in a verification failure.

1. **Caching (`CachingClient`)**:
   - Checks if the request is cacheable and if a valid local copy exists.
2. **Request Signing (`SecureDeviceSigningClient`)**:
   - **Payload Construction**: Concatenates `METHOD|PATH|TIMESTAMP|PLAINTEXT_BODY`.
   - **Signature**: Computes ECDSA P-256 signature using the device-specific private key.
   - **Header Injection**: Adds `X-App-Device-Id` and `X-App-Signature` headers.
3. **ALE Encryption (`AleClient`)**:
   - **Key Generation**: Generates a cryptographically secure 32-byte (256-bit) AES session key.
   - **Wrapping**: Encrypts the AES key with the Server RSA Public Key (RSA-OAEP SHA-256).
   - **Encryption**: Encrypts the JSON body using AES-256-GCM (generating a 12-byte IV and 16-byte Auth Tag).
   - **Header Injection**: Adds `X-ALE-Enabled: true` and `X-ALE-Session-Key` (Base64 wrapped key).
4. **Transport**:
   - The final payload is transmitted over **HTTPS**.

### 4.3. Phase 3: Incoming Response & Cleanup
1. **Response Interception**: The `AleClient` proactively attempts to decrypt any **2xx** response if an ALE session key was generated for the request. While it respects the `X-ALE-Enabled` header, it does not strictly require it for decryption of successful responses to ensure resilience against header stripping.
2. **Decryption**:
   - **String-First Treatment**: The response body is treated as a potential JSON-quoted string. It is first unwrapped using `jsonDecode` to extract the raw Base64 payload if the server delivered it as a string.
   - **AES-GCM**: Using the *same* AES session key, it decrypts the payload (`IV (12) + Tag (16) + Ciphertext (n)`).
3. **Zero-Memory Cleanup**:
   - The `AleClient` executes a `finally` block to manually overwrite the AES session key `Uint8List` with zeros.
   - The request/response cycle is finalized, and the plaintext data is passed to the Bloc.

### 4.4. Response Parsing Robustness
The application employs a **Safe Double-Decoding** pattern in `ApiBaseMixin`. This logic handles cases where the decrypted body might be delivered as a JSON string containing further JSON. It only performs a second-level decode if the first result is a string that explicitly looks like a JSON object (`{`) or array (`[`), preventing character-position format errors on plaintext data.

## 5. Data Persistence
- **Tokens/Keys**: Written to `flutter_secure_storage`.
- **UI Preferences**: Written to `shared_preferences`.
- **Memory**: Sensitive values (like passwords during login) are converted to `Uint8List` and cleared immediately after the API call.
- **Global State**: Global Cubits (Theme, Locale, Settings) listen to the authentication state stream and automatically reset their in-memory values to defaults upon logout, preventing preference leakage to the next user.
