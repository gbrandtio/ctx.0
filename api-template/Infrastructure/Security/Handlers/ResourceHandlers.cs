using System.Security.Claims;
using Domain.Constants;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Security.Handlers;

/// <summary>
/// Automated IDOR protection (AUTHORIZATION.md §5): route parameters with
/// the standard names ({projectId}, {userId}, {orgId}, {orgUserId},
/// {memberUserId}) are verified against JWT claims — stateless where
/// possible, hierarchical DB lookup for multi-org owners. Registered
/// SCOPED so they can use the request's DbContext.
/// </summary>
public static class RouteValues
{
    public static long? Get(HttpContext? httpContext, string name) =>
        httpContext?.Request.RouteValues.TryGetValue(name, out var value) == true &&
        long.TryParse(value?.ToString(), out var id)
            ? id
            : null;
}

public sealed class ProjectResourceHandler(IHttpContextAccessor httpContextAccessor, AppDbContext db)
    : AuthorizationHandler<ProjectRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, ProjectRequirement requirement)
    {
        var projectId = RouteValues.Get(httpContextAccessor.HttpContext, "projectId");
        if (projectId is null)
        {
            return; // no {projectId} in the route — nothing to verify
        }

        var role = context.User.FindFirstValue(ClaimTypes.Role);

        // Stateless path: member users carry their projectId in the JWT.
        if (!requirement.OrgOwnersOnly &&
            role == SecurityConstants.Roles.MemberUser &&
            context.User.FindFirstValue(SecurityConstants.ClaimTypes.ProjectId) ==
                projectId.Value.ToString())
        {
            context.Succeed(requirement);
            return;
        }

        // Hierarchical path (AUTHORIZATION.md §5): an OrgUser may own many
        // organizations — resolve the project's parent org and match it
        // against the JWT orgId claims.
        if (role == SecurityConstants.Roles.OrgUser)
        {
            var orgIds = context.User.FindAll(SecurityConstants.ClaimTypes.OrgId)
                .Select(c => c.Value)
                .ToHashSet();
            var parentOrgId = await db.Projects
                .Where(p => p.Id == projectId.Value)
                .Select(p => (long?)p.OrgId)
                .FirstOrDefaultAsync();
            if (parentOrgId is not null && orgIds.Contains(parentOrgId.Value.ToString()))
            {
                context.Succeed(requirement);
            }
        }
    }
}

public sealed class UserResourceHandler(IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<UserSelfRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, UserSelfRequirement requirement)
    {
        var userId = RouteValues.Get(httpContextAccessor.HttpContext, "userId");
        if (userId is not null &&
            context.User.FindFirstValue(SecurityConstants.ClaimTypes.UserId) ==
                userId.Value.ToString())
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}

public sealed class OrgUserResourceHandler(IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<OrgUserSelfRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, OrgUserSelfRequirement requirement)
    {
        var orgUserId = RouteValues.Get(httpContextAccessor.HttpContext, "orgUserId");
        if (orgUserId is not null &&
            context.User.FindFirstValue(SecurityConstants.ClaimTypes.UserId) ==
                orgUserId.Value.ToString())
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}

public sealed class OrgResourceHandler(IHttpContextAccessor httpContextAccessor)
    : AuthorizationHandler<OrgRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, OrgRequirement requirement)
    {
        var orgId = RouteValues.Get(httpContextAccessor.HttpContext, "orgId");
        if (orgId is not null &&
            context.User.FindAll(SecurityConstants.ClaimTypes.OrgId)
                .Any(c => c.Value == orgId.Value.ToString()))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}

public sealed class MemberUserResourceHandler(
    IHttpContextAccessor httpContextAccessor, AppDbContext db)
    : AuthorizationHandler<MemberUserManagementRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, MemberUserManagementRequirement requirement)
    {
        var memberUserId = RouteValues.Get(httpContextAccessor.HttpContext, "memberUserId");
        if (memberUserId is null)
        {
            return;
        }

        // Self-service path.
        if (context.User.FindFirstValue(SecurityConstants.ClaimTypes.UserId) ==
            memberUserId.Value.ToString())
        {
            context.Succeed(requirement);
            return;
        }

        // Org-owner path (AUTHORIZATION.md §7).
        if (context.User.FindFirstValue(ClaimTypes.Role) == SecurityConstants.Roles.OrgUser)
        {
            var orgIds = context.User.FindAll(SecurityConstants.ClaimTypes.OrgId)
                .Select(c => c.Value)
                .ToHashSet();
            var memberOrgId = await db.MemberUsers
                .Where(m => m.Id == memberUserId.Value)
                .Select(m => (long?)m.OrgId)
                .FirstOrDefaultAsync();
            if (memberOrgId is not null && orgIds.Contains(memberOrgId.Value.ToString()))
            {
                context.Succeed(requirement);
            }
        }
    }
}
