using Domain.Constants;

namespace Infrastructure.Security;

/// <summary>
/// The "Rbac" configuration section (AUTHORIZATION.md §4). Both maps are
/// merged over the built-in defaults: an entry with an existing name
/// replaces the default; a new name defines a custom role or assignment.
/// </summary>
public sealed class RbacOptions
{
    public const string SectionName = "Rbac";

    /// <summary>Grant role name → permissions ("*" grants everything).</summary>
    public Dictionary<string, string[]> Roles { get; init; } = [];

    /// <summary>
    /// Principal key ("Role" or "Role:Type", matching the JWT role/type
    /// claims) → grant roles whose permissions are unioned.
    /// </summary>
    public Dictionary<string, string[]> Assignments { get; init; } = [];
}

/// <summary>
/// The single source of truth for role → permission grants
/// (AUTHORIZATION.md §4). Ships the default grant roles every CRUD app
/// needs (Admin, ReadWrite, ReadSelf, Payments) plus the default principal
/// assignments; both are extendable/overridable via the Rbac configuration
/// section. Construction validates every definition, so an undefined role
/// or misspelled permission aborts startup instead of misbehaving at
/// request time.
/// </summary>
public sealed class RoleCatalog
{
    private static readonly Dictionary<string, string[]> DefaultRoles = new()
    {
        [SecurityConstants.GrantRoles.Admin] = [SecurityConstants.Permissions.All],
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
    };

    private static readonly Dictionary<string, string[]> DefaultAssignments = new()
    {
        [$"{SecurityConstants.Roles.OrgUser}:{SecurityConstants.OrgUserTypes.Admin}"] =
            [SecurityConstants.GrantRoles.Admin],
        [SecurityConstants.Roles.MemberUser] = [SecurityConstants.GrantRoles.ReadWrite],
        [SecurityConstants.Roles.User] =
            [SecurityConstants.GrantRoles.ReadSelf, SecurityConstants.GrantRoles.Payments],
    };

    private readonly Dictionary<string, IReadOnlySet<string>> _roles;
    private readonly Dictionary<string, IReadOnlySet<string>> _assignments;

    public RoleCatalog() : this(new RbacOptions()) { }

    public RoleCatalog(RbacOptions options)
    {
        _roles = Merge(DefaultRoles, options.Roles)
            .ToDictionary(kvp => kvp.Key, IReadOnlySet<string> (kvp) =>
                kvp.Value.ToHashSet(StringComparer.Ordinal));
        Validate();

        _assignments = Merge(DefaultAssignments, options.Assignments)
            .ToDictionary(kvp => kvp.Key, ResolveAssignment);
    }

    /// <summary>Grant role names and their permission sets (for docs/diagnostics).</summary>
    public IReadOnlyDictionary<string, IReadOnlySet<string>> Roles => _roles;

    public bool IsDefined(string grantRole) => _roles.ContainsKey(grantRole);

    /// <summary>
    /// True when the principal (JWT role + optional type claim) is granted
    /// the permission. Unassigned/unknown principals hold no permissions.
    /// </summary>
    public bool HasPermission(string? principalRole, string? principalType, string permission)
    {
        if (principalRole is null)
        {
            return false;
        }

        var granted =
            (principalType is not null
                && _assignments.TryGetValue($"{principalRole}:{principalType}", out var byType))
                    ? byType
            : _assignments.TryGetValue(principalRole, out var byRole) ? byRole
            // A JWT role naming a grant role directly is honoured, so custom
            // principals can skip the assignment map entirely.
            : _roles.TryGetValue(principalRole, out var direct) ? direct
            : null;

        return granted is not null
            && (granted.Contains(SecurityConstants.Permissions.All)
                || granted.Contains(permission));
    }

    private static Dictionary<string, string[]> Merge(
        Dictionary<string, string[]> defaults, Dictionary<string, string[]> overrides)
    {
        var merged = new Dictionary<string, string[]>(defaults, StringComparer.Ordinal);
        foreach (var (key, value) in overrides)
        {
            merged[key] = value;
        }
        return merged;
    }

    private void Validate()
    {
        foreach (var (role, permissions) in _roles)
        {
            var unknown = permissions
                .Where(p => p != SecurityConstants.Permissions.All
                    && !SecurityConstants.Permissions.Known.Contains(p))
                .ToList();
            if (unknown.Count > 0)
            {
                throw new InvalidOperationException(
                    $"RBAC role '{role}' references unknown permission(s): " +
                    $"{string.Join(", ", unknown)}. Add them to " +
                    "SecurityConstants.Permissions or fix the Rbac:Roles configuration.");
            }
        }
    }

    private IReadOnlySet<string> ResolveAssignment(KeyValuePair<string, string[]> assignment)
    {
        var undefined = assignment.Value.Where(r => !_roles.ContainsKey(r)).ToList();
        if (undefined.Count > 0)
        {
            throw new InvalidOperationException(
                $"RBAC assignment '{assignment.Key}' references undefined role(s): " +
                $"{string.Join(", ", undefined)}. Every assignment must use a role " +
                "defined in the catalog (defaults or Rbac:Roles).");
        }

        return assignment.Value
            .SelectMany(r => _roles[r])
            .ToHashSet(StringComparer.Ordinal);
    }
}
