using System.Security.Cryptography;
using System.Text;
using CtxApp.Application.Abstractions;

namespace CtxApp.Infrastructure.Security.Envelope;

/// <summary>HMAC-SHA256 blind index over the normalized (trim + lower-case) value.</summary>
public sealed class HmacBlindIndex : IBlindIndex
{
    private readonly byte[] _key;

    public HmacBlindIndex(EnvelopeOptions options)
    {
        if (string.IsNullOrEmpty(options.BlindIndexKey))
        {
            throw new InvalidOperationException("CTX_ENVELOPE_BLIND_INDEX_KEY is not configured. Run `ctx0 keygen`.");
        }
        _key = Convert.FromBase64String(options.BlindIndexKey);
    }

    public string Compute(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        var hash = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexStringLower(hash);
    }
}
