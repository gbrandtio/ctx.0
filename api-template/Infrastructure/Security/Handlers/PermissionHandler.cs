using System.Security.Claims;
using Domain.Constants;
using Microsoft.AspNetCore.Authorization;

namespace Infrastructure.Security.Handlers;

/// <summary>
/// Grants a PermissionRequirement when the RoleCatalog assigns the JWT's
/// principal (role + type claims) a grant role holding the permission
/// (AUTHORIZATION.md §4, §6). Roles and assignments are configured in the
/// catalog, never here.
/// </summary>
public sealed class PermissionHandler(RoleCatalog catalog)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var role = context.User.FindFirstValue(ClaimTypes.Role);
        var type = context.User.FindFirstValue(SecurityConstants.ClaimTypes.UserType);

        if (catalog.HasPermission(role, type, requirement.Permission))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
