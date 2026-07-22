using CtxApp.Application.Abstractions;
using CtxApp.Domain.Security;

namespace CtxApp.Application.Security;

/// <summary>
/// Issues and rotates refresh tokens with theft detection. Each login starts a
/// token family; every refresh rotates the active token and revokes the old one.
/// Presenting an already-rotated token means it was stolen, so the whole family
/// is revoked. Storage-agnostic: all persistence is behind
/// <see cref="IRefreshTokenStore"/>, so the reuse logic is unit-testable.
/// </summary>
public sealed class RefreshTokenService(
    IRefreshTokenStore store,
    IJwtIssuer jwt,
    ITokenGenerator generator,
    ITokenHasher hasher,
    IClock clock,
    RefreshTokenTtl ttl)
{
    /// <summary>Start a new token family for a freshly authenticated user.</summary>
    public Task<AuthTokens> IssueAsync(Guid userId, CancellationToken cancellationToken = default)
        => IssueInFamilyAsync(userId, Guid.NewGuid(), cancellationToken);

    /// <summary>Rotate a presented refresh token, detecting reuse.</summary>
    public async Task<AuthTokens> RotateAsync(string presentedRefreshToken, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var existing = await store.FindByHashAsync(hasher.Hash(presentedRefreshToken), cancellationToken);

        if (existing is null)
        {
            throw new AuthException("Invalid refresh token.");
        }
        if (existing.RevokedAt is not null)
        {
            // The token was already rotated: this is a replay of a stolen token.
            await store.RevokeFamilyAsync(existing.FamilyId, now, cancellationToken);
            await store.SaveChangesAsync(cancellationToken);
            throw new AuthException("Refresh token reuse detected; session revoked.");
        }
        if (!existing.IsActive(now))
        {
            throw new AuthException("Refresh token expired.");
        }

        var (tokens, replacement) = BuildTokens(existing.UserId, existing.FamilyId, now);
        existing.RevokedAt = now;
        existing.ReplacedByTokenId = replacement.Id;
        await store.AddAsync(replacement, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return tokens;
    }

    /// <summary>Revoke the whole family a token belongs to (logout).</summary>
    public async Task RevokeAsync(string presentedRefreshToken, CancellationToken cancellationToken = default)
    {
        var existing = await store.FindByHashAsync(hasher.Hash(presentedRefreshToken), cancellationToken);
        if (existing is not null)
        {
            await store.RevokeFamilyAsync(existing.FamilyId, clock.UtcNow, cancellationToken);
            await store.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<AuthTokens> IssueInFamilyAsync(Guid userId, Guid familyId, CancellationToken cancellationToken)
    {
        var (tokens, token) = BuildTokens(userId, familyId, clock.UtcNow);
        await store.AddAsync(token, cancellationToken);
        await store.SaveChangesAsync(cancellationToken);
        return tokens;
    }

    private (AuthTokens Tokens, RefreshToken Token) BuildTokens(Guid userId, Guid familyId, DateTimeOffset now)
    {
        var (access, accessExpires) = jwt.Issue(userId);
        var raw = generator.NewToken();
        var token = new RefreshToken
        {
            UserId = userId,
            FamilyId = familyId,
            TokenHash = hasher.Hash(raw),
            CreatedAt = now,
            ExpiresAt = now + ttl.Value,
        };
        return (new AuthTokens(access, accessExpires, raw, token.ExpiresAt), token);
    }
}
