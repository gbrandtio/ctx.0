using Domain.Constants;
using Infrastructure.Security;

using AppApi.Extensions;

namespace UnitTests.Security;

public sealed class RoleCatalogTests
{
    [Fact]
    public void Default_assignments_preserve_the_documented_grants()
    {
        // The shipped catalog must reproduce AUTHORIZATION.md §4 exactly:
        // changing a default grant is a security-relevant, deliberate act.
        var catalog = new RoleCatalog(SecurityExtensions.BuildRoleCatalogSeed());

        Assert.True(catalog.HasPermission(
            SecurityConstants.Roles.OrgUser, SecurityConstants.OrgUserTypes.Admin,
            SecurityConstants.Permissions.ProjectsDelete));

        Assert.True(catalog.HasPermission(
            SecurityConstants.Roles.MemberUser, null,
            SecurityConstants.Permissions.OrdersManage));
        Assert.False(catalog.HasPermission(
            SecurityConstants.Roles.MemberUser, null,
            SecurityConstants.Permissions.ProjectsUpdate));

        Assert.True(catalog.HasPermission(
            SecurityConstants.Roles.User, null,
            SecurityConstants.Permissions.PaymentsProcess));
        Assert.False(catalog.HasPermission(
            SecurityConstants.Roles.User, null,
            SecurityConstants.Permissions.OrdersManage));
    }

    [Fact]
    public void Unknown_or_unassigned_principals_hold_no_permissions()
    {
        var catalog = new RoleCatalog(SecurityExtensions.BuildRoleCatalogSeed());

        Assert.False(catalog.HasPermission("Ghost", null,
            SecurityConstants.Permissions.ProjectsRead));
        Assert.False(catalog.HasPermission(null, null,
            SecurityConstants.Permissions.ProjectsRead));
        // OrgUser without the Admin type has no assignment → no grants.
        Assert.False(catalog.HasPermission(SecurityConstants.Roles.OrgUser, null,
            SecurityConstants.Permissions.ProjectsRead));
    }

    [Fact]
    public void Custom_role_and_assignment_from_configuration_are_honoured()
    {
        var catalog = new RoleCatalog(SecurityExtensions.BuildRoleCatalogSeed(), new RbacOptions
        {
            Roles = new() { ["Analyst"] = [SecurityConstants.Permissions.AnalyticsView] },
            Assignments = new()
            {
                ["Support"] = ["Analyst", SecurityConstants.GrantRoles.ReadSelf],
            },
        });

        // Assignments union the permissions of every listed grant role.
        Assert.True(catalog.HasPermission("Support", null,
            SecurityConstants.Permissions.AnalyticsView));
        Assert.True(catalog.HasPermission("Support", null,
            SecurityConstants.Permissions.OrdersView));
        Assert.False(catalog.HasPermission("Support", null,
            SecurityConstants.Permissions.OrdersManage));

        // A JWT role naming a grant role directly needs no assignment.
        Assert.True(catalog.HasPermission("Analyst", null,
            SecurityConstants.Permissions.AnalyticsView));
    }

    [Fact]
    public void Configuration_can_override_a_default_role()
    {
        var catalog = new RoleCatalog(SecurityExtensions.BuildRoleCatalogSeed(), new RbacOptions
        {
            Roles = new()
            {
                [SecurityConstants.GrantRoles.ReadSelf] =
                    [SecurityConstants.Permissions.OrdersView],
            },
        });

        Assert.False(catalog.HasPermission(SecurityConstants.Roles.User, null,
            SecurityConstants.Permissions.ProjectsRead));
        Assert.True(catalog.HasPermission(SecurityConstants.Roles.User, null,
            SecurityConstants.Permissions.OrdersView));
    }

    [Fact]
    public void Misspelled_permission_in_a_role_aborts_startup()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => new RoleCatalog(
            SecurityExtensions.BuildRoleCatalogSeed(),
            new RbacOptions { Roles = new() { ["Broken"] = ["projects:reed"] } }));
        Assert.Contains("projects:reed", ex.Message);
    }

    [Fact]
    public void Assignment_to_an_undefined_role_aborts_startup()
    {
        // "Users must use one of the defined roles" — enforced at startup.
        var ex = Assert.Throws<InvalidOperationException>(() => new RoleCatalog(
            SecurityExtensions.BuildRoleCatalogSeed(),
            new RbacOptions { Assignments = new() { ["Support"] = ["NotARole"] } }));
        Assert.Contains("NotARole", ex.Message);
    }

    [Fact]
    public void Admin_wildcard_covers_permissions_added_later()
    {
        var catalog = new RoleCatalog(SecurityExtensions.BuildRoleCatalogSeed());
        foreach (var permission in SecurityConstants.Permissions.Known)
        {
            Assert.True(catalog.HasPermission(
                SecurityConstants.Roles.OrgUser, SecurityConstants.OrgUserTypes.Admin,
                permission));
        }
    }
}
