using Microsoft.Extensions.Configuration;

namespace CtxApp.Infrastructure.Security;

/// <summary>
/// Rate-limit settings read from the <c>CTX_RATE_LIMIT_*</c> environment
/// variables. Requests are partitioned by authenticated user id, or by client IP
/// when unauthenticated.
/// </summary>
public sealed class RateLimitOptions
{
    /// <summary>Requests permitted per window per partition.</summary>
    public int PermitLimit { get; init; } = 100;

    /// <summary>Fixed window length in seconds.</summary>
    public int WindowSeconds { get; init; } = 60;

    /// <summary>Read the options from the <c>CTX_RATE_LIMIT_*</c> environment variables, falling back to defaults.</summary>
    public static RateLimitOptions FromConfiguration(IConfiguration configuration)
    {
        var defaults = new RateLimitOptions();
        return new RateLimitOptions
        {
            PermitLimit = ParseInt(configuration["CTX_RATE_LIMIT_PERMIT_LIMIT"], defaults.PermitLimit),
            WindowSeconds = ParseInt(configuration["CTX_RATE_LIMIT_WINDOW_SECONDS"], defaults.WindowSeconds),
        };
    }

    private static int ParseInt(string? value, int fallback) =>
        int.TryParse(value, out var parsed) ? parsed : fallback;
}
