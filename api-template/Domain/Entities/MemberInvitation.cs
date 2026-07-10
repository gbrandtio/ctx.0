namespace Domain.Entities;

/// <summary>Invitation for a member user to join a project.</summary>
public class MemberInvitation
{
    public long Id { get; set; }
    public long OrgId { get; set; }
    public long ProjectId { get; set; }
    public string EmailHash { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public Project? Project { get; set; }
}
