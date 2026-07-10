namespace Application.Abstractions;

/// <summary>Claims baked into an access token (AUTHENTICATION.md — JWT claims).</summary>
public sealed record AccessTokenSubject(
    long UserId,
    string Username,
    string Role,
    string? Type = null,
    IReadOnlyList<long>? OrgIds = null,
    long? ProjectId = null);

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateAccessToken(AccessTokenSubject subject);

    /// <summary>64 random bytes, Base64 — the opaque refresh token.</summary>
    string GenerateRefreshToken();

    /// <summary>SHA-256 hex of a refresh token; only hashes are persisted.</summary>
    string HashRefreshToken(string refreshToken);
}
