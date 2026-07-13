namespace Domain.Constants;

/// <summary>
/// The RBAC vocabulary (AUTHORIZATION.md). Permissions follow
/// resource:action; policies combine permissions with resource-ownership
/// requirements. Extend per AUTHORIZATION.md §9 — add constants here, grant
/// them to roles in the RoleCatalog, register the policy in
/// SecurityExtensions.
/// </summary>
public static class SecurityConstants
{
    /// <summary>
    /// Principal roles: WHO the caller is. Carried in the JWT role claim and
    /// used by resource-ownership handlers and RLS. What a principal MAY DO
    /// comes from the grant role it is assigned to in the RoleCatalog.
    /// </summary>
    public static class Roles
    {
        public const string User = "User";
        public const string MemberUser = "MemberUser";
        public const string OrgUser = "OrgUser";
    }

    /// <summary>
    /// Default grant roles shipped with the template: named permission
    /// bundles common to every CRUD app. Principals map to one of these via
    /// RoleCatalog assignments; custom roles are added via the Rbac
    /// configuration section (AUTHORIZATION.md §4).
    /// </summary>
    public static class GrantRoles
    {
        /// <summary>Full access — every permission (wildcard).</summary>
        public const string Admin = "Admin";

        /// <summary>Read and write within the caller's own scope.</summary>
        public const string ReadWrite = "ReadWrite";

        /// <summary>Read-only over the caller's own resources (consumer tier).</summary>
        public const string ReadSelf = "ReadSelf";

        /// <summary>Pay against server-issued orders (consumer tier).</summary>
        public const string Payments = "Payments";
    }

    public static class OrgUserTypes
    {
        public const string Admin = "Admin";
    }

    /// <summary>Aliases of the plane's CtxClaimTypes — one vocabulary,
    /// no drift between token issuance and app policies.</summary>
    public static class ClaimTypes
    {
        public const string UserId = CtxClaimTypes.UserId;
        public const string OrgId = CtxClaimTypes.OrgId;
        public const string ProjectId = CtxClaimTypes.ProjectId;
        public const string UserType = CtxClaimTypes.UserType;
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

        /// <summary>Wildcard: grants every permission (Admin).</summary>
        public const string All = "*";

        /// <summary>
        /// Every valid permission. The RoleCatalog validates role
        /// definitions against this set at startup, so a typo in a custom
        /// role fails fast instead of silently denying access.
        /// </summary>
        public static readonly IReadOnlySet<string> Known = new HashSet<string>
        {
            ProjectsRead, ProjectsWrite, ProjectsUpdate, ProjectsDelete,
            AnalyticsView, OrdersView, OrdersManage, PaymentsProcess,
        };
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
