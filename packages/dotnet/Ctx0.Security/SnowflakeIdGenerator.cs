using Ctx0.Security.Abstractions;

namespace Ctx0.Security;

/// <summary>
/// Time-based snowflake IDs: 41 bits of milliseconds since a custom
/// epoch, 10 bits of node id, 12 bits of per-millisecond sequence.
/// Singleton; thread-safe.
/// </summary>
public sealed class SnowflakeIdGenerator(int nodeId = 0) : IIdGenerator
{
    private static readonly DateTime Epoch =
        new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly Lock _lock = new();
    private readonly long _nodeId = nodeId & 0x3FF;
    private long _lastTimestamp = -1;
    private long _sequence;

    public long NextId()
    {
        lock (_lock)
        {
            var timestamp = (long)(DateTime.UtcNow - Epoch).TotalMilliseconds;
            if (timestamp == _lastTimestamp)
            {
                _sequence = (_sequence + 1) & 0xFFF;
                if (_sequence == 0)
                {
                    while (timestamp <= _lastTimestamp)
                    {
                        timestamp = (long)(DateTime.UtcNow - Epoch).TotalMilliseconds;
                    }
                }
            }
            else
            {
                _sequence = 0;
            }
            _lastTimestamp = timestamp;
            return (timestamp << 22) | (_nodeId << 12) | _sequence;
        }
    }
}
