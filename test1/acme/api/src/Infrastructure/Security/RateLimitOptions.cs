namespace Acme.Infrastructure.Security;

/// <summary>
/// Rate-limit settings bound from the <c>Ctx:RateLimit</c> configuration
/// section. Requests are partitioned by authenticated user id, or by client IP
/// when unauthenticated.
/// </summary>
public sealed class RateLimitOptions
{
    public const string Section = "Ctx:RateLimit";

    /// <summary>Requests permitted per window per partition.</summary>
    public int PermitLimit { get; init; } = 100;

    /// <summary>Fixed window length in seconds.</summary>
    public int WindowSeconds { get; init; } = 60;
}
