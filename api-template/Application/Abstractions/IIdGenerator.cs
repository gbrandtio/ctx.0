namespace Application.Abstractions;

/// <summary>Time-based snowflake IDs for bigint primary keys.</summary>
public interface IIdGenerator
{
    long NextId();
}
