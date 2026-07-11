namespace Domain.Entities;

/// <summary>
/// Per-user aggregate counters, updated atomically via ExecuteUpdateAsync
/// (EFCORE_ADVANCED_PERFORMANCE_TOPICS.md §6) — never read-modify-write.
/// </summary>
public class UserTotals
{
    public long UserId { get; set; }
    public long OrdersPaid { get; set; }
    public long TotalSpentMinor { get; set; }
    public DateTime UpdatedAt { get; set; }
}
