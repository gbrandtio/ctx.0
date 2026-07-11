namespace Domain.Entities;

/// <summary>
/// Rotating refresh token (AUTHENTICATION.md — Refresh Token Security).
/// Only the SHA-256 hash is stored; family_id links rotations to their
/// original login so reuse detection can revoke the whole family.
/// </summary>
public class RefreshToken
{
    public long Id { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public long UserId { get; set; }

    /// <summary>Principal type discriminator ("User", "MemberUser", "OrgUser").</summary>
    public string UserType { get; set; } = string.Empty;

    public Guid FamilyId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime CreatedAt { get; set; }
}
