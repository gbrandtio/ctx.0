using Microsoft.AspNetCore.Authorization;

namespace Infrastructure.Security.Handlers;

/// <summary>JWT projectId (member) or org ownership (org user) must match route {projectId}.</summary>
public sealed class ProjectRequirement(bool orgOwnersOnly = false) : IAuthorizationRequirement
{
    /// <summary>OrgProjectOwner policy: member users are NOT sufficient.</summary>
    public bool OrgOwnersOnly { get; } = orgOwnersOnly;
}

/// <summary>JWT uid must match route {userId} (UserSelf).</summary>
public sealed class UserSelfRequirement : IAuthorizationRequirement;

/// <summary>JWT uid must match route {orgUserId} (OrgUserSelf).</summary>
public sealed class OrgUserSelfRequirement : IAuthorizationRequirement;

/// <summary>Route {orgId} must be one of the JWT orgId claims.</summary>
public sealed class OrgRequirement : IAuthorizationRequirement;

/// <summary>JWT uid matches {memberUserId} OR requester owns the member's organization.</summary>
public sealed class MemberUserManagementRequirement : IAuthorizationRequirement;
