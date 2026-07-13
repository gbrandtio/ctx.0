using System.Security.Claims;
using Ctx0.Security.Abstractions;
using Microsoft.AspNetCore.Authorization;

namespace Ctx0.Security;

/// <summary>Grants access when the JWT's role maps to the permission.</summary>
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

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
        var type = context.User.FindFirstValue(CtxClaimTypes.UserType);

        if (catalog.HasPermission(role, type, requirement.Permission))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
