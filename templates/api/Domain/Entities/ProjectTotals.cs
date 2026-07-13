namespace Domain.Entities;

/// <summary>Per-project aggregates, updated atomically via ExecuteUpdateAsync.</summary>
public class ProjectTotals
{
    public long ProjectId { get; set; }
    public long OrdersPaid { get; set; }
    public long RevenueMinor { get; set; }
    public DateTime UpdatedAt { get; set; }
}
