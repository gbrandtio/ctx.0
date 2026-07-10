namespace Domain.Entities;

/// <summary>
/// Parent tenant of the example hierarchy Organization → Project
/// (AUTHORIZATION.md). Rename Project to your domain's child resource;
/// keep the mechanism.
/// </summary>
public class Organization
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>The OrgUser who owns this organization.</summary>
    public long OwnerId { get; set; }

    public DateTime CreatedAt { get; set; }

    public List<Project> Projects { get; set; } = [];
}
