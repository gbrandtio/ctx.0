
namespace UnitTests;

public sealed class TestClock(DateTime? now = null) : IClock
{
    public DateTime UtcNow { get; set; } = now ?? new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
}
