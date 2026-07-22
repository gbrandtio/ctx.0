using Microsoft.Extensions.Configuration;

namespace CtxApp.Infrastructure.Security.Jwt;

/// <summary>JWT settings read from the <c>CTX_JWT_*</c> environment variables.</summary>
public sealed class JwtOptions
{
    public string Issuer { get; init; } = "ctxapp";
    public string Audience { get; init; } = "ctxapp";

    /// <summary>HMAC-SHA256 signing key (at least 32 characters). Provided via the environment.</summary>
    public string SigningKey { get; init; } = string.Empty;

    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 14;

    /// <summary>Read the options from the <c>CTX_JWT_*</c> environment variables, falling back to defaults.</summary>
    public static JwtOptions FromConfiguration(IConfiguration configuration)
    {
        var defaults = new JwtOptions();
        return new JwtOptions
        {
            Issuer = configuration["CTX_JWT_ISSUER"] ?? defaults.Issuer,
            Audience = configuration["CTX_JWT_AUDIENCE"] ?? defaults.Audience,
            SigningKey = configuration["CTX_JWT_SIGNING_KEY"] ?? defaults.SigningKey,
            AccessTokenMinutes = ParseInt(configuration["CTX_JWT_ACCESS_TOKEN_MINUTES"], defaults.AccessTokenMinutes),
            RefreshTokenDays = ParseInt(configuration["CTX_JWT_REFRESH_TOKEN_DAYS"], defaults.RefreshTokenDays),
        };
    }

    private static int ParseInt(string? value, int fallback) =>
        int.TryParse(value, out var parsed) ? parsed : fallback;
}
