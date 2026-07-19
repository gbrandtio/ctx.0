namespace CtxApp.Infrastructure.Security.Jwt;

/// <summary>JWT settings bound from the <c>Ctx:Jwt</c> configuration section.</summary>
public sealed class JwtOptions
{
    public const string Section = "Ctx:Jwt";

    public string Issuer { get; init; } = "ctxapp";
    public string Audience { get; init; } = "ctxapp";

    /// <summary>HMAC-SHA256 signing key (at least 32 characters). Provided via the environment.</summary>
    public string SigningKey { get; init; } = string.Empty;

    public int AccessTokenMinutes { get; init; } = 15;
    public int RefreshTokenDays { get; init; } = 14;
}
