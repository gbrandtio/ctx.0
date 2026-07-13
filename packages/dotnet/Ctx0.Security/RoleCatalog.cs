using Ctx0.Security.Abstractions;

namespace Ctx0.Security;

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
    private readonly Dictionary<string, IReadOnlySet<string>> _roles;
    private readonly Dictionary<string, IReadOnlySet<string>> _assignments;

    /// <summary>"*" grants every permission.</summary>
    public const string WildcardPermission = "*";

    private readonly RoleCatalogSeed _seed;

    public RoleCatalog(RoleCatalogSeed seed) : this(seed, new RbacOptions()) { }

    public RoleCatalog(RoleCatalogSeed seed, RbacOptions options)
    {
        _seed = seed;
        _roles = Merge(seed.DefaultRoles, options.Roles)
            .ToDictionary(kvp => kvp.Key, IReadOnlySet<string> (kvp) =>
                kvp.Value.ToHashSet(StringComparer.Ordinal));
        Validate();

        _assignments = Merge(seed.DefaultAssignments, options.Assignments)
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
            && (granted.Contains(WildcardPermission)
                || granted.Contains(permission));
    }

    private static Dictionary<string, string[]> Merge(
        IReadOnlyDictionary<string, string[]> defaults, Dictionary<string, string[]> overrides)
    {
        var merged = defaults.ToDictionary(
            kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);
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
                .Where(p => p != WildcardPermission
                    && !_seed.KnownPermissions.Contains(p))
                .ToList();
            if (unknown.Count > 0)
            {
                throw new InvalidOperationException(
                    $"RBAC role '{role}' references unknown permission(s): " +
                    $"{string.Join(", ", unknown)}. Add them to " +
                    "the RoleCatalogSeed.KnownPermissions or fix the Rbac:Roles configuration.");
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
