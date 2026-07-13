using System.Security.Cryptography;
using System.Text;
using Ctx0.Security.Abstractions;
using Microsoft.Extensions.Options;

namespace Ctx0.Security;

/// <summary>
/// Deterministic HMAC-SHA256 signatures for searchable encrypted fields
/// (ENVELOPE_ENCRYPTION_ARCHITECTURE.md §2 — Blind Indexes). Uses a
/// dedicated key distinct from the KEKs.
/// </summary>
public sealed class BlindIndexProvider : IBlindIndexProvider
{
    private readonly byte[] _key;

    public BlindIndexProvider(IOptions<EncryptionOptions> options)
    {
        var key = options.Value.BlindIndexKey;
        if (string.IsNullOrEmpty(key))
        {
            throw new InvalidOperationException(
                "Security:Encryption:BlindIndexKey is required.");
        }
        _key = Convert.FromBase64String(key);
    }

    public string ComputeHash(string value) =>
        Convert.ToHexStringLower(
            HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(value)));
}
