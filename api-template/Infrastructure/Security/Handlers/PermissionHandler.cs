using System.Security.Claims;
using Domain.Constants;
using Microsoft.AspNetCore.Authorization;

namespace Infrastructure.Security.Handlers;

/// <summary>
/// Maps the JWT role/type claims to granular permissions
/// (AUTHORIZATION.md §5). Extend the switch when adding roles or
/// permissions (§8).
/// </summary>
public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    private static readonly string[] MemberUserPermissions =
    [
        SecurityConstants.Permissions.ProjectsRead,
        SecurityConstants.Permissions.AnalyticsView,
        SecurityConstants.Permissions.OrdersManage,
        SecurityConstants.Permissions.OrdersView,
    ];

    private static readonly string[] UserPermissions =
    [
        SecurityConstants.Permissions.ProjectsRead,
        SecurityConstants.Permissions.OrdersView,
        SecurityConstants.Permissions.PaymentsProcess,
    ];

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var role = context.User.FindFirstValue(ClaimTypes.Role);
        var type = context.User.FindFirstValue(SecurityConstants.ClaimTypes.UserType);

        var granted = role switch
        {
            // Org admins are granted all permissions.
            SecurityConstants.Roles.OrgUser
                when type == SecurityConstants.OrgUserTypes.Admin => true,
            SecurityConstants.Roles.MemberUser =>
                MemberUserPermissions.Contains(requirement.Permission),
            SecurityConstants.Roles.User =>
                UserPermissions.Contains(requirement.Permission),
            _ => false,
        };

        if (granted)
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
