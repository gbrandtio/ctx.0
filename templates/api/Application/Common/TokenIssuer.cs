using Application.Abstractions;
using Contracts.Auth;
using Domain.Entities;

namespace Application.Common;

/// <summary>
/// Shared token-pair issuance (AUTHENTICATION.md): JWT access token +
/// rotating refresh token persisted as a SHA-256 hash within a family.
/// </summary>
public sealed class TokenIssuer(
    IJwtTokenService jwt,
    IRefreshTokenRepository refreshTokens,
    IIdGenerator ids,
    IClock clock)
{
    public TimeSpan RefreshTokenLifetime { get; init; } = TimeSpan.FromDays(30);

    public async Task<AuthResponse> IssueAsync(
        AccessTokenSubject subject,
        string email,
        Guid? familyId,
        CancellationToken ct)
    {
        var (accessToken, expiresAt) = jwt.CreateAccessToken(subject);
        var refreshToken = jwt.GenerateRefreshToken();

        refreshTokens.Add(new RefreshToken
        {
            Id = ids.NextId(),
            TokenHash = jwt.HashRefreshToken(refreshToken),
            UserId = subject.UserId,
            UserType = subject.Role,
            FamilyId = familyId ?? Guid.NewGuid(),
            ExpiresAt = clock.UtcNow.Add(RefreshTokenLifetime),
            IsRevoked = false,
            CreatedAt = clock.UtcNow,
        });
        await refreshTokens.SaveChangesAsync(ct);

        return new AuthResponse(
            accessToken, refreshToken, expiresAt, subject.UserId, subject.Username, email);
    }
}
