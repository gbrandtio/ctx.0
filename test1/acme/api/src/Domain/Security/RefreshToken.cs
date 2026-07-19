namespace Acme.Domain.Security;

/// <summary>
/// A stored refresh token. The raw token is never persisted — only its hash.
/// Tokens form rotation families keyed by <see cref="FamilyId"/>: presenting a
/// token that was already rotated (revoked) signals theft and revokes the whole
/// family.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required Guid UserId { get; init; }

    /// <summary>Groups all tokens rotated from one original login.</summary>
    public required Guid FamilyId { get; init; }

    /// <summary>Hash of the opaque token value.</summary>
    public required string TokenHash { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>Set when the token is rotated or revoked; a used token cannot be reused.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>The token that replaced this one on rotation, if any.</summary>
    public Guid? ReplacedByTokenId { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;
}
