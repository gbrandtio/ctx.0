using Ctx0.Security;
using Ctx0.Security.Abstractions;
using Domain.Constants;
using Infrastructure.Security.Handlers;
using Microsoft.AspNetCore.Authorization;

namespace AppApi.Extensions;

/// <summary>
/// App-side security wiring on top of the Ctx0.Security plane: the RBAC
/// seed (this app's roles/permissions vocabulary), the resource-ownership
/// handlers, and the authorization policies (AUTHORIZATION.md §5). New
/// feature permissions register their policy here (§9). The plane itself
/// (JWT, ALE, signing, rate limiting, RLS identity) comes from
/// AddCtxSecurity/UseCtxSecurity and is never wired manually.
/// </summary>
public static class SecurityExtensions
{
    /// <summary>
    /// This app's RBAC vocabulary: the default grant roles every CRUD app
    /// needs and the default principal assignments, validated against
    /// SecurityConstants.Permissions.Known at startup.
    /// </summary>
    public static RoleCatalogSeed BuildRoleCatalogSeed() => new()
    {
        DefaultRoles = new Dictionary<string, string[]>
        {
            [SecurityConstants.GrantRoles.Admin] = [RoleCatalog.WildcardPermission],
            [SecurityConstants.GrantRoles.ReadWrite] =
            [
                SecurityConstants.Permissions.ProjectsRead,
                SecurityConstants.Permissions.AnalyticsView,
                SecurityConstants.Permissions.OrdersManage,
                SecurityConstants.Permissions.OrdersView,
            ],
            [SecurityConstants.GrantRoles.ReadSelf] =
            [
                SecurityConstants.Permissions.ProjectsRead,
                SecurityConstants.Permissions.OrdersView,
            ],
            [SecurityConstants.GrantRoles.Payments] =
            [
                SecurityConstants.Permissions.PaymentsProcess,
            ],
        },
        DefaultAssignments = new Dictionary<string, string[]>
        {
            [$"{SecurityConstants.Roles.OrgUser}:{SecurityConstants.OrgUserTypes.Admin}"] =
                [SecurityConstants.GrantRoles.Admin],
            [SecurityConstants.Roles.MemberUser] = [SecurityConstants.GrantRoles.ReadWrite],
            [SecurityConstants.Roles.User] =
                [SecurityConstants.GrantRoles.ReadSelf, SecurityConstants.GrantRoles.Payments],
        },
        KnownPermissions = SecurityConstants.Permissions.Known,
    };

    public static IServiceCollection AddAppSecurity(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCtxSecurity(configuration, BuildRoleCatalogSeed());

        // Resource handlers are scoped (they use the request DbContext
        // for hierarchical verification).
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

        return services;
    }

    /// <summary>
    /// The whole security pipeline in its contractual order — a single
    /// delegation to the plane (APPLICATION_LAYER_SECURITY.md).
    /// </summary>
    public static IApplicationBuilder UseAppSecurity(this IApplicationBuilder app) =>
        app.UseCtxSecurity();
}
