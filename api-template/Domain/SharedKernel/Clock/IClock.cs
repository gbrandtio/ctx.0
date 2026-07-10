namespace SharedKernel.Clock;

/// <summary>
/// Injectable time source (ARCHITECTURE_OVERVIEW.md — Global Coding
/// Standards): application logic never calls DateTime.UtcNow directly so
/// tests can time-travel. Always UTC.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
