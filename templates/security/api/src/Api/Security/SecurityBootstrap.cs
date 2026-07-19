using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using CtxApp.Application.Abstractions;
using CtxApp.Application.Security;
using CtxApp.Infrastructure.Security;
using CtxApp.Infrastructure.Security.Envelope;
using CtxApp.Infrastructure.Security.Jwt;
using CtxApp.Infrastructure.Security.Passwords;
using CtxApp.Infrastructure.Security.Rls;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace CtxApp.Api.Security;

/// <summary>
/// Registration surface for the vendored ctx.0 security plane. The base
/// <c>Program.cs</c> calls <see cref="AddCtxSecurity"/> during service
/// configuration, <see cref="UseCtxSecurity"/> in the pipeline, and maps the
/// security endpoints with
/// <see cref="CtxSecurityEndpoints.MapCtxSecurityEndpoints"/>.
/// </summary>
public static class SecurityBootstrap
{
    public static IServiceCollection AddCtxSecurity(this IServiceCollection services, IConfiguration config)
    {
        var jwt = new JwtOptions();
        config.GetSection(JwtOptions.Section).Bind(jwt);
        if (jwt.SigningKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Ctx:Jwt:SigningKey must be at least 32 characters. Run `ctx0 keygen`.");
        }

        services.AddSingleton(jwt);
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ITokenGenerator, RandomTokenGenerator>();
        services.AddSingleton<ITokenHasher, Sha256TokenHasher>();
        services.AddSingleton<IJwtIssuer, JwtIssuer>();
        services.AddSingleton(new RefreshTokenTtl(TimeSpan.FromDays(jwt.RefreshTokenDays)));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        services.AddSingleton<IDeviceKeyRegistry, InMemoryDeviceKeyRegistry>();
        services.AddSingleton<IAleKeyProvider, ConfigAleKeyProvider>();

        // Encryption at rest: envelope encryption + blind indexes.
        var envelope = new EnvelopeOptions();
        config.GetSection(EnvelopeOptions.Section).Bind(envelope);
        services.AddSingleton(envelope);
        services.AddSingleton<IFieldCipher, EnvelopeFieldCipher>();
        services.AddSingleton<IBlindIndex, HmacBlindIndex>();

        // Row-Level Security: per-request session GUC + policy initialization.
        services.AddScoped<RlsConnectionInterceptor>();
        services.AddScoped<IInterceptor>(sp => sp.GetRequiredService<RlsConnectionInterceptor>());
        services.AddHostedService<RlsInitializer>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });
        services.AddAuthorization();

        // Rate limiting: per authenticated user, else per client IP.
        var rate = new RateLimitOptions();
        config.GetSection(RateLimitOptions.Section).Bind(rate);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http =>
            {
                var userId = http.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                    ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var partitionKey = userId is not null
                    ? $"user:{userId}"
                    : $"ip:{http.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = rate.PermitLimit,
                    Window = TimeSpan.FromSeconds(rate.WindowSeconds),
                    QueueLimit = 0,
                });
            });
        });

        return services;
    }

    /// <summary>Add the authentication/authorization + rate-limiting middleware. Call before mapping endpoints.</summary>
    public static WebApplication UseCtxSecurity(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        return app;
    }
}
