using System.Text;
using System.Threading.RateLimiting;
using Domain.Constants;
using Infrastructure.Security;
using Infrastructure.Security.Handlers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace AppApi.Extensions;

/// <summary>
/// AuthN (JWT bearer), AuthZ policies (AUTHORIZATION.md §5), and the
/// partitioned rate limiter (AUTHENTICATION.md — Infrastructure Security).
/// New feature permissions register their policy here (§9).
/// </summary>
public static class SecurityExtensions
{
    public static IServiceCollection AddAppSecurity(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();

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

        // Role catalog: defaults merged with the Rbac config section;
        // construction validates definitions so bad config aborts startup.
        var rbacOptions = configuration.GetSection(RbacOptions.SectionName)
            .Get<RbacOptions>() ?? new RbacOptions();
        services.AddSingleton(new RoleCatalog(rbacOptions));

        // Permission handler is stateless; resource handlers are scoped
        // (they use the request DbContext for hierarchical verification).
        services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
        services.AddScoped<IAuthorizationHandler, ProjectResourceHandler>();
        services.AddScoped<IAuthorizationHandler, UserResourceHandler>();
        services.AddScoped<IAuthorizationHandler, OrgResourceHandler>();
        services.AddScoped<IAuthorizationHandler, OrgUserResourceHandler>();
        services.AddScoped<IAuthorizationHandler, MemberUserResourceHandler>();

        services.AddAuthorizationBuilder()
            .AddPolicy(SecurityConstants.Policies.UserSelf,
                p => p.RequireAuthenticatedUser().AddRequirements(new UserSelfRequirement()))
            .AddPolicy(SecurityConstants.Policies.ProjectRead,
                p => p.RequireAuthenticatedUser().AddRequirements(
                    new PermissionRequirement(SecurityConstants.Permissions.ProjectsRead),
                    new ProjectRequirement()))
            .AddPolicy(SecurityConstants.Policies.ProjectWrite,
                p => p.RequireAuthenticatedUser().AddRequirements(
                    new PermissionRequirement(SecurityConstants.Permissions.ProjectsUpdate),
                    new ProjectRequirement()))
            .AddPolicy(SecurityConstants.Policies.ProjectAnalytics,
                p => p.RequireAuthenticatedUser().AddRequirements(
                    new PermissionRequirement(SecurityConstants.Permissions.AnalyticsView),
                    new ProjectRequirement()))
            .AddPolicy(SecurityConstants.Policies.OrderManage,
                p => p.RequireAuthenticatedUser().AddRequirements(
                    new PermissionRequirement(SecurityConstants.Permissions.OrdersManage),
                    new ProjectRequirement()))
            .AddPolicy(SecurityConstants.Policies.PaymentProcess,
                p => p.RequireAuthenticatedUser()
                    .RequireRole(SecurityConstants.Roles.User)
                    .AddRequirements(new PermissionRequirement(
                        SecurityConstants.Permissions.PaymentsProcess)))
            .AddPolicy(SecurityConstants.Policies.OrgAdmin,
                p => p.RequireAuthenticatedUser().AddRequirements(
                    new PermissionRequirement(SecurityConstants.Permissions.ProjectsWrite),
                    new OrgRequirement()))
            .AddPolicy(SecurityConstants.Policies.OrgUserSelf,
                p => p.RequireAuthenticatedUser().AddRequirements(new OrgUserSelfRequirement()))
            .AddPolicy(SecurityConstants.Policies.MemberUserManagement,
                p => p.RequireAuthenticatedUser().AddRequirements(
                    new MemberUserManagementRequirement()))
            .AddPolicy(SecurityConstants.Policies.OrgAdminOnly,
                p => p.RequireAuthenticatedUser()
                    .RequireRole(SecurityConstants.Roles.OrgUser)
                    .RequireClaim(SecurityConstants.ClaimTypes.UserType,
                        SecurityConstants.OrgUserTypes.Admin))
            .AddPolicy(SecurityConstants.Policies.OrgProjectOwner,
                p => p.RequireAuthenticatedUser()
                    .RequireRole(SecurityConstants.Roles.OrgUser)
                    .AddRequirements(new ProjectRequirement(orgOwnersOnly: true)));

        services.AddAppRateLimiting(configuration);
        return services;
    }

    /// <summary>
    /// Partitioned rate limiting: per user identity when authenticated,
    /// per IP when anonymous; queue limit 0 → immediate 429.
    /// </summary>
    private static IServiceCollection AddAppRateLimiting(
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
            ? $"user:{context.User.FindFirst(SecurityConstants.ClaimTypes.UserId)?.Value}"
            : $"ip:{RemoteIp(context)}";

    private static string RemoteIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
