namespace Domain.Constants;

/// <summary>
/// The RBAC vocabulary (AUTHORIZATION.md). Permissions follow
/// resource:action; policies combine permissions with resource-ownership
/// requirements. Extend per AUTHORIZATION.md §8 — add constants here, map
/// them in PermissionHandler, register the policy in SecurityExtensions.
/// </summary>
public static class SecurityConstants
{
    public static class Roles
    {
        public const string User = "User";
        public const string MemberUser = "MemberUser";
        public const string OrgUser = "OrgUser";
    }

    public static class OrgUserTypes
    {
        public const string Admin = "Admin";
    }

    public static class ClaimTypes
    {
        public const string UserId = "uid";
        public const string OrgId = "orgId";
        public const string ProjectId = "projectId";
        public const string UserType = "type";
    }

    public static class Permissions
    {
        public const string ProjectsRead = "projects:read";
        public const string ProjectsWrite = "projects:write";
        public const string ProjectsUpdate = "projects:update";
        public const string ProjectsDelete = "projects:delete";
        public const string AnalyticsView = "analytics:view";
        public const string OrdersView = "orders:view";
        public const string OrdersManage = "orders:manage";
        public const string PaymentsProcess = "payments:process";
    }

    public static class Policies
    {
        public const string UserSelf = "UserSelf";
        public const string ProjectRead = "ProjectRead";
        public const string ProjectWrite = "ProjectWrite";
        public const string ProjectAnalytics = "ProjectAnalytics";
        public const string OrderManage = "OrderManage";
        public const string PaymentProcess = "PaymentProcess";
        public const string OrgAdmin = "OrgAdmin";
        public const string OrgUserSelf = "OrgUserSelf";
        public const string MemberUserManagement = "MemberUserManagement";
        public const string OrgAdminOnly = "OrgAdminOnly";
        public const string OrgProjectOwner = "OrgProjectOwner";
    }
}
