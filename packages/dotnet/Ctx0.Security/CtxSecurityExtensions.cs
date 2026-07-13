using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Ctx0.Security.Abstractions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Ctx0.Security;

/// <summary>
/// The single wiring seam of the server security plane. Consumers call
/// <see cref="AddCtxSecurity"/> with their RBAC seed and then register
/// app-specific authorization handlers/policies on top, and
/// <see cref="UseCtxSecurity"/> exactly once in the pipeline. The
/// internal order — ALE decryption → signature verification →
/// authentication → RLS identity propagation → authorization → rate
/// limiting — is contractual (APPLICATION_LAYER_SECURITY.md); never
/// register these components individually or reorder them.
/// </summary>
public static class CtxSecurityExtensions
{
    public static IServiceCollection AddCtxSecurity(
        this IServiceCollection services,
        IConfiguration configuration,
        RoleCatalogSeed roleCatalogSeed)
    {
        services.AddHttpContextAccessor();

        // ---- Options (env-var secrets; validated at startup) ----
        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => o.SigningKey.Length >= 32,
                "Jwt:SigningKey must be at least 32 characters (JWT_SIGNING_KEY).")
            .ValidateOnStart();
        services.AddOptions<EncryptionOptions>()
            .Bind(configuration.GetSection(EncryptionOptions.SectionName))
            .Validate(o => !string.IsNullOrEmpty(o.CurrentVersion) &&
                           o.Keys.ContainsKey(o.CurrentVersion),
                "Security:Encryption must define CurrentVersion and its key.")
            .ValidateOnStart();
        services.Configure<AleOptions>(configuration.GetSection(AleOptions.SectionName));
        // Identifier-only names: they are interpolated into set_config /
        // SET ROLE SQL (values stay parameterized).
        services.AddOptions<RlsOptions>()
            .Bind(configuration.GetSection(RlsOptions.SectionName))
            .Validate(o => IsSqlIdentifier(o.SettingName) && IsSqlIdentifier(o.WorkerRole),
                "Security:Rls names must be lowercase SQL identifiers.")
            .ValidateOnStart();

        // ---- Core security services ----
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<AesEncryptionProvider>();
        services.AddSingleton<IBlindIndexProvider, BlindIndexProvider>();
        services.AddSingleton<CurrentUserContext>();
        services.AddSingleton<ICurrentUserProvider>(
            sp => sp.GetRequiredService<CurrentUserContext>());
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IGoogleTokenValidator, GoogleTokenValidator>();
        services.AddSingleton(sp => new AleCryptoService(
            configuration[$"{AleOptions.SectionName}:RsaPrivateKey"]
                ?? throw new InvalidOperationException(
                    "Security:Ale:RsaPrivateKey is required.")));
        services.AddSingleton<IIdGenerator>(
            new SnowflakeIdGenerator(configuration.GetValue("NodeId", 0)));

        // ---- AuthN ----
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = configuration["Jwt:Issuer"] ?? "app-api",
                    ValidateAudience = true,
                    ValidAudience = configuration["Jwt:Audience"] ?? "app-mobile-client",
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(configuration["Jwt:SigningKey"] ?? string.Empty)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        // ---- RBAC: consumer seed merged with the Rbac config section;
        // construction validates definitions so bad config aborts startup.
        var rbacOptions = configuration.GetSection(RbacOptions.SectionName)
            .Get<RbacOptions>() ?? new RbacOptions();
        services.AddSingleton(new RoleCatalog(roleCatalogSeed, rbacOptions));
        services.AddSingleton<IAuthorizationHandler, PermissionHandler>();

        services.AddCtxRateLimiting(configuration);
        return services;
    }

    /// <summary>
    /// The request-security pipeline in its contractual order. Also
    /// advertises the wire-protocol version (X-Ctx-Protocol) so clients
    /// and `ctx0 doctor` can verify mobile/API compatibility.
    /// </summary>
    public static IApplicationBuilder UseCtxSecurity(this IApplicationBuilder app)
    {
        app.Use((context, next) =>
        {
            context.Response.Headers[CtxProtocol.HeaderName] = CtxProtocol.Version;
            return next(context);
        });

        app.UseMiddleware<AleMiddleware>();
        app.UseMiddleware<RequestSigningMiddleware>();

        app.UseAuthentication();

        // RLS identity: expose the JWT uid to the RLS interceptor for
        // this request's async flow.
        app.Use((context, next) =>
        {
            var userContext = context.RequestServices.GetRequiredService<CurrentUserContext>();
            var uid = context.User.FindFirst(CtxClaimTypes.UserId)?.Value;
            userContext.SetUser(long.TryParse(uid, out var id) ? id : null);
            return next(context);
        });

        app.UseAuthorization();
        app.UseRateLimiter();
        return app;
    }

    /// <summary>
    /// Partitioned rate limiting: per user identity when authenticated,
    /// per IP when anonymous; queue limit 0 → immediate 429. Auth and
    /// account-creation endpoints opt into their stricter named policies.
    /// </summary>
    private static IServiceCollection AddCtxRateLimiting(
        this IServiceCollection services, IConfiguration configuration)
    {
        var permitLimit = configuration.GetValue("RateLimiting:PermitLimit", 100);
        var windowSeconds = configuration.GetValue("RateLimiting:WindowSeconds", 60);

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                context => RateLimitPartition.GetFixedWindowLimiter(
                    PartitionKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromSeconds(windowSeconds),
                        QueueLimit = 0,
                    }));

            limiter.AddPolicy("auth", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    RemoteIp(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 200,
                        Window = TimeSpan.FromMinutes(5),
                        QueueLimit = 0,
                    }));

            limiter.AddPolicy("account_creation", context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    RemoteIp(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 50,
                        Window = TimeSpan.FromHours(1),
                        QueueLimit = 0,
                    }));
        });
        return services;
    }

    private static string PartitionKey(HttpContext context) =>
        context.User.Identity?.IsAuthenticated == true
            ? $"user:{context.User.FindFirst(CtxClaimTypes.UserId)?.Value}"
            : $"ip:{RemoteIp(context)}";

    private static string RemoteIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    private static bool IsSqlIdentifier(string value) =>
        value.Length > 0 && value.All(
            c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '_' or '.');
}
