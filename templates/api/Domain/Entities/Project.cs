namespace Domain.Entities;

/// <summary>
/// Child resource of the example hierarchy (a store, team, workspace, …).
/// Rename to your domain's unit in a single consistent change
/// (AUTHORIZATION.md §9).
/// </summary>
public class Project
{
    public long Id { get; set; }
    public long OrgId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public Organization? Organization { get; set; }
    public ProjectTotals? Totals { get; set; }
}
