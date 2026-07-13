using System.Security.Cryptography;
using System.Text;
using Ctx0.Security.Abstractions;
using Microsoft.Extensions.Options;

namespace Ctx0.Security;

/// <summary>
/// AES-256-GCM primitives for envelope encryption
/// (ENVELOPE_ENCRYPTION_ARCHITECTURE.md §2). Ciphertext layout is always
/// [12-byte nonce][16-byte tag][ciphertext], nonce fresh per operation.
/// DEK wrapping prefixes the KEK version ("v1:...") to support
/// zero-downtime rotation.
/// </summary>
public sealed class AesEncryptionProvider(IOptions<EncryptionOptions> options)
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly EncryptionOptions _options = options.Value;

    public byte[] GenerateDek() => RandomNumberGenerator.GetBytes(32);

    /// <summary>Wraps a DEK with the current KEK → "vN:{base64 blob}".</summary>
    public string WrapDek(byte[] dek)
    {
        var version = _options.CurrentVersion;
        var kek = GetKek(version);
        return $"{version}:{Convert.ToBase64String(EncryptBytes(kek, dek))}";
    }

    /// <summary>Unwraps a version-prefixed DEK using the matching KEK.</summary>
    public byte[] UnwrapDek(string wrappedDek)
    {
        var separator = wrappedDek.IndexOf(':');
        if (separator <= 0)
        {
            throw new CryptographicException("Encrypted DEK is missing its KEK version prefix.");
        }
        var version = wrappedDek[..separator];
        var blob = Convert.FromBase64String(wrappedDek[(separator + 1)..]);
        return DecryptBytes(GetKek(version), blob);
    }

    public bool IsCurrentVersion(string wrappedDek) =>
        wrappedDek.StartsWith(_options.CurrentVersion + ":", StringComparison.Ordinal);

    public string EncryptString(byte[] dek, string plaintext) =>
        Convert.ToBase64String(EncryptBytes(dek, Encoding.UTF8.GetBytes(plaintext)));

    public string DecryptString(byte[] dek, string ciphertext) =>
        Encoding.UTF8.GetString(DecryptBytes(dek, Convert.FromBase64String(ciphertext)));

    public static byte[] EncryptBytes(byte[] key, byte[] plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);
        return [.. nonce, .. tag, .. ciphertext];
    }

    public static byte[] DecryptBytes(byte[] key, byte[] blob)
    {
        if (blob.Length < NonceSize + TagSize)
        {
            throw new CryptographicException("Ciphertext blob is too short.");
        }
        var nonce = blob.AsSpan(0, NonceSize);
        var tag = blob.AsSpan(NonceSize, TagSize);
        var ciphertext = blob.AsSpan(NonceSize + TagSize);
        var plaintext = new byte[ciphertext.Length];
        using var aes = new AesGcm(key.AsSpan(), TagSize);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);
        return plaintext;
    }

    private byte[] GetKek(string version)
    {
        if (!_options.Keys.TryGetValue(version, out var entry) ||
            string.IsNullOrEmpty(entry.Key))
        {
            throw new CryptographicException($"No KEK configured for version '{version}'.");
        }
        var kek = Convert.FromBase64String(entry.Key);
        return kek.Length == 32
            ? kek
            : throw new CryptographicException("KEK must be 32 bytes (Base64).");
    }
}
