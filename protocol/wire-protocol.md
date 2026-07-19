# ctx.0 Wire Protocol v1.0

The contract between the Flutter app (`app/`) and the .NET API (`api/`). Both
sides implement this document and assert against the golden vectors in
`vectors.json` (synced into each workspace at `.ctx/vectors.json`).

## Keys and encodings

- Curve: NIST P-256 (secp256r1).
- Private key: raw 32-byte big-endian scalar, base64.
- Public key: uncompressed point `0x04 || X[32] || Y[32]` (65 bytes), base64.
- All binary fields on the wire are standard base64.

## Headers

| Header | Value |
|---|---|
| `X-Ctx-Protocol` | `1.0`. A mismatch is rejected. |
| `X-Ctx-Device-Id` | The enrolled device identifier. |
| `X-Ctx-Timestamp` | Unix time in milliseconds. Requests outside a 5-minute window are rejected. |
| `X-Ctx-Signature` | Base64 ECDSA P-256 signature (see Signing). |

## Enrollment

- `GET /v1/security/ale-public-key` → `{ "publicKey": <base64 uncompressed> }`:
  the server's static ALE public key.
- `POST /v1/security/devices` with `{ "deviceId", "publicKey" }`: registers the
  ECDSA public key the device signs requests with.

## ALE (Application-Layer Encryption)

ECIES over P-256 with AES-256-GCM:

1. The sender generates an ephemeral P-256 key pair and computes the ECDH shared
   secret with the recipient's static public key. The secret is the 32-byte
   big-endian X coordinate of the shared point.
2. Key = `HKDF-SHA256(ikm = sharedX, salt = 32 zero bytes, info = "ctx-ale-v1", L = 32)`.
3. Body = `AES-256-GCM(key, iv = 12 random bytes, plaintext, aad = empty)`,
   producing a ciphertext and a 16-byte tag.

Request envelope (JSON): `{ "Epk", "Iv", "Ct", "Tag" }`, where `Epk` is the
sender's ephemeral public key. The server derives the same key from its static
private key and `Epk`. The response reuses that key and omits `Epk`:
`{ "Iv", "Ct", "Tag" }`.

## Signing

The signature covers a canonical string over the exact bytes on the wire:

```
<HTTP-METHOD uppercase>\n
<request path + query>\n
<X-Ctx-Timestamp>\n
<lowercase-hex SHA-256 of the request body bytes>
```

Algorithm: ECDSA P-256 with SHA-256, IEEE P1363 fixed-width (`r || s`, 64 bytes)
encoding, base64. The client uses deterministic ECDSA (RFC 6979); the server
accepts any valid signature. The body hashed is the ALE envelope JSON bytes.

## Golden vectors (`.ctx/vectors.json`)

`ale`: `serverPrivateB64`, `serverPublicB64`, `ephemeralPrivateB64`,
`ephemeralPublicB64`, `ivB64`, `plaintextUtf8`, `derivedKeyB64`, `ciphertextB64`,
`tagB64`. `signing`: `devicePrivateB64`, `devicePublicB64`, `method`, `path`,
`timestamp`, `bodyUtf8`, `bodySha256Hex`, `canonicalString`, `signatureB64`.
Both test suites reproduce these values and verify the recorded signature.
