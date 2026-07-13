
namespace Domain.Entities;

/// <summary>
/// Organization owner/back-office principal. May own multiple
/// Organizations (JWT carries one orgId claim per owned org). PII is
/// envelope-encrypted like User.
/// </summary>
public class OrgUser
{
    public long Id { get; set; }

    // ---- PII (encrypted at rest) ----
    [CtxEncrypted] public string Email { get; set; } = string.Empty;
    [CtxEncrypted] public string? Name { get; set; }

    public string EmailHash { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string EncryptedDek { get; set; } = string.Empty;

    /// <summary>Sub-role, e.g. "Admin" (SecurityConstants.OrgUserTypes).</summary>
    public string Type { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}
