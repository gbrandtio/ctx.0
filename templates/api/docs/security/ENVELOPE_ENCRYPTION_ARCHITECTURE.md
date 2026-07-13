# Envelope Encryption Architecture

This document details the Envelope Encryption implementation for the API. It covers the cryptographic mechanics, integration with Entity Framework Core, searchability via Blind Indexes, and strict guidelines on Key Management.

---

## 1. What is Envelope Encryption?

Envelope Encryption is a cryptographic pattern used to secure data at scale. Instead of encrypting all data with a single master key (which increases the blast radius if the key is compromised and makes key rotation difficult), Envelope Encryption uses a two-tier key system:

1. **Data Encryption Key (DEK):** A unique key generated to encrypt a specific piece of data (in our case, a single row/user).
2. **Key Encryption Key (KEK):** A "Master Key" used *only* to encrypt the DEKs.

By storing the *encrypted* DEK alongside the encrypted data, the application only needs access to the KEK to unlock the DEK, which in turn unlocks the data.

---

## 2. Implementation Mechanics

### The Cryptographic Flow
- **Algorithm:** AES-256-GCM (Galois/Counter Mode). This provides both confidentiality and data authenticity, preventing padding oracle attacks and ciphertext tampering.
- **Dynamic Nonces & Authentication Tags:** A 12-byte cryptographically secure random nonce is generated for *every single encryption operation* — **both** when encrypting PII with a DEK **and** when wrapping a DEK with the KEK. After encryption, a 16-byte authentication tag is generated. The ciphertext structure is always `[12-byte Nonce] + [16-byte Tag] + [Ciphertext]`. This guarantees that encrypting the exact same input twice results in completely different ciphertexts, neutralizing frequency analysis attacks, while the tag ensures data integrity.

> [!CAUTION]
> **Never configure a static IV/nonce.** AES-GCM's security collapses catastrophically under nonce reuse with the same key (it leaks the XOR of plaintexts and enables authentication-key recovery). The KEK configuration therefore consists of the key **only**; nonces are always generated per-operation and stored inside the ciphertext blob.

- **DEK Generation:** For every new `User` (or other PII-bearing principal) created, `AesEncryptionProvider` generates a fresh, random 32-byte (256-bit) DEK.
- **Interception:** The encryption logic is entirely decoupled from the business logic via EF Core Interceptors (`EnvelopeEncryptionInterceptor`).
- **Participation:** An entity opts in by annotating its PII string properties with `[CtxEncrypted]` (`Ctx0.Security.Abstractions`) and exposing a string `EncryptedDek` property. The interceptor discovers annotated properties by reflection (cached per type) — it has no knowledge of concrete entity types and there is no central registry to extend.

### How Reads/Writes Work
1. **Writing (SavingChanges):** 
   - EF Core intercepts the save command.
   - If the user is new, a 32-byte DEK is generated.
   - The DEK is encrypted (wrapped) using the KEK — with a fresh random nonce — and assigned to the `EncryptedDek` property.
   - The user's PII (`Name`, `Email`, etc.) is encrypted using the plaintext DEK, producing a fresh 12-byte nonce and a 16-byte tag prepended to the ciphertext.
   - To avoid corrupting the application state in memory, the Interceptor temporarily caches the plaintext values, allows EF Core to save the ciphertext to PostgreSQL, and immediately restores the plaintext to the C# objects (`SavedChanges`).
     > **Technical Note on Interceptor State:** To prevent EF Core's `ManyServiceProvidersCreatedWarning`, the `EnvelopeEncryptionInterceptor` is registered as a **Singleton**. To safely maintain plaintext state concurrently across multiple HTTP requests without memory leaks, the interceptor uses a thread-safe `ConditionalWeakTable<DbContext, Dictionary<...>>`. This binds the temporary state directly to the lifecycle of the per-request `DbContext` instance.
2. **Reading (InitializedInstance):**
   - When EF Core pulls a row from PostgreSQL, the Interceptor hooks into the materialization phase.
   - It reads `EncryptedDek` and decrypts it via the KEK to get the plaintext DEK.
   - It iterates over the PII properties, extracts the 12-byte nonce and 16-byte tag from the front of the decoded Base64 byte array, and decrypts and authenticates the data using the DEK.

### Searchability (Blind Indexes)
Because the encryption uses random nonces and random DEKs, it is **non-deterministic**. `Where(x => x.Email == "test@test.com")` will fail because the database stores random ciphertext.

To solve this, we use **Blind Indexes**. The `BlindIndexProvider` uses `HMAC-SHA256` (keyed hash, with a dedicated index key distinct from the KEK) to generate a deterministic signature for searchable fields.
- When a user is saved, `EmailHash = HMAC(Email)` is stored.
- When searching, the repository generates the HMAC of the search input and queries the database via `Where(x => x.EmailHash == inputHash)`.

---

## 3. Key Management: Where are DEK and KEK stored?

The security of Envelope Encryption completely depends on where and how the keys are stored.

### Data Encryption Key (DEK)
- **Where it is stored:** The DEK is stored **directly in the PostgreSQL database**, alongside the user data, in the `encrypted_dek` column.
- **Is this safe?** Yes. Because the DEK is encrypted using the KEK before it is written to the database, an attacker who steals a database dump only gets encrypted data and encrypted keys. Without the KEK, the DEK is useless.

### Key Encryption Key (KEK)
The KEK is the absolute "keys to the kingdom." If the KEK is compromised, the entire database is compromised.

- **Default (template) storage:** Injected into the application via the `EncryptionOptions` class, which pulls from environment secrets (`Security__Encryption__*`, see Section 4).
- **Where it SHOULD be stored in Production:**
  Storing the KEK in an environment variable on the hosting server is considered a medium-to-high risk in enterprise environments. An attacker gaining shell access or reading environment variables can steal the KEK.

> [!CAUTION]
> **Production Best Practices for KEK**
> The KEK should almost never reside in the application's memory or configuration files. It should be offloaded to a dedicated Key Management Service (KMS).

**Recommended Cloud Providers:**
1. **Azure Key Vault**
2. **AWS KMS (Key Management Service)**
3. **HashiCorp Vault**
4. **Google Cloud KMS**

**How KMS Integration Changes the Architecture:**
Instead of `AesEncryptionProvider` holding the KEK and running AES locally to encrypt the DEK:
1. The application generates the DEK locally.
2. The application sends the plaintext DEK over a secure TLS connection to the KMS provider via an API call.
3. The KMS provider encrypts the DEK using a Master Key stored in a Hardware Security Module (HSM). The KMS provider never exposes the Master Key to the application.
4. The KMS provider returns the Encrypted DEK to the application.
5. The application saves the Encrypted DEK to the database.

**Key Rotation:**
When using a KMS, you can rotate the Master KEK at any time. When the KEK is rotated, you do not need to re-encrypt all the PII data in the database. You only need to write a background script that fetches all `encrypted_dek` fields, asks the KMS to decrypt them with the old KEK, asks the KMS to re-encrypt them with the new KEK, and updates the database. The actual user data remains untouched.

---

## 4. Key Rotation Setup and Execution

For deployments relying on application-level environment secrets rather than a managed KMS, the template implements a **Zero-Downtime Key Versioning** architecture.

This allows you to rotate your master KEKs simply by updating configuration variables and restarting the application. A built-in `KekRotationWorker` automatically upgrades old DEKs to the newest KEK on startup.

### First-Time Setup (Development)

To enable Key Versioning in your local development environment, use the .NET `user-secrets` tool to define the current version and map the key value to it.

Run the following commands in the `AppApi` project folder:

```bash
# Set the current active key version
dotnet user-secrets set "Security:Encryption:CurrentVersion" "v1"

# Set the Key for version "v1" (32 random bytes, Base64)
dotnet user-secrets set "Security:Encryption:Keys:v1:Key" "<your_base64_key>"
```

### First-Time Setup (Production)

In your hosting provider, mimic the `user-secrets` hierarchy by using double-underscores (`__`) to represent nested JSON properties.

Set these Environment Variables:
- `Security__Encryption__CurrentVersion` = `v1`
- `Security__Encryption__Keys__v1__Key` = `<your_base64_key>`

### How to Rotate the Master KEK (Zero Downtime)

When you are ready to rotate your encryption keys (e.g., rotating from `v1` to `v2`), follow these steps:

1. **Generate a new Key:**
   Generate a new cryptographically secure 256-bit key and encode it in Base64.
2. **Add the new key alongside the old key:**
   Keep your `v1` key exactly where it is. Add the new `v2` key to your environment secrets.
   * `Security__Encryption__Keys__v2__Key` = `<new_key>`
3. **Promote the new version:**
   Update the current version pointer to the new version.
   * `Security__Encryption__CurrentVersion` = `v2`
4. **Deploy / Restart:**
   Restart your application. 

**What happens next?**
- As soon as the API starts, the `KekRotationWorker` (a background hosted service) boots up.
- It scans the database in small batches for any principal whose `encrypted_dek` does *not* start with `v2:`.
- It loads the entity, decrypts the DEK using the `v1` key, and forces an Entity Framework update.
- The `EnvelopeEncryptionInterceptor` catches the update and seamlessly re-encrypts the DEK using the active `v2` key (with a fresh nonce).
- At the same time, any user that actively logs in or modifies their profile will also have their DEK silently upgraded by the interceptor.

Once you are absolutely certain that no `v1:` prefixes remain in your database, you can safely delete the `v1` key from your environment variables.
