
namespace Domain.Entities;

/// <summary>
/// Principal working within one Project (AUTHORIZATION.md). PII is
/// envelope-encrypted like User.
/// </summary>
public class MemberUser
{
    public long Id { get; set; }
    public long OrgId { get; set; }
    public long ProjectId { get; set; }

    // ---- PII (encrypted at rest) ----
    [CtxEncrypted] public string Email { get; set; } = string.Empty;
    [CtxEncrypted] public string? Name { get; set; }

    public string EmailHash { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string EncryptedDek { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public Project? Project { get; set; }
}
