namespace Acme.Application.Abstractions;

/// <summary>Generates cryptographically-random opaque tokens (e.g. refresh tokens).</summary>
public interface ITokenGenerator
{
    /// <summary>A new URL-safe random token string.</summary>
    string NewToken();
}

/// <summary>Hashes opaque tokens for at-rest storage (never store the raw token).</summary>
public interface ITokenHasher
{
    string Hash(string token);
}

/// <summary>Abstracts the current time so token lifetimes are testable.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
