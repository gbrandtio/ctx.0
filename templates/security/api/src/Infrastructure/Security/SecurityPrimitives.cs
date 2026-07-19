using System.Security.Cryptography;
using CtxApp.Application.Abstractions;

namespace CtxApp.Infrastructure.Security;

/// <summary>256-bit URL-safe random opaque tokens.</summary>
public sealed class RandomTokenGenerator : ITokenGenerator
{
    public string NewToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}

/// <summary>Hashes opaque tokens with SHA-256 for at-rest storage.</summary>
public sealed class Sha256TokenHasher : ITokenHasher
{
    public string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));
}

/// <summary>The system clock.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
