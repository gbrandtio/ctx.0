namespace Ctx0.Security.Abstractions;

/// <summary>JWT configuration.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "app-api";
    public string Audience { get; set; } = "app-mobile-client";

    /// <summary>HS256 key, ≥ 32 chars, injected via JWT_SIGNING_KEY.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 30;
}

/// <summary>ALE + request-signing configuration.</summary>
public sealed class AleOptions
{
    public const string SectionName = "Security:Ale";

    /// <summary>PEM content or a file path; literal \n sequences are normalized.</summary>
    public string RsaPrivateKey { get; set; } = string.Empty;
    public string RsaPublicKey { get; set; } = string.Empty;

    public bool Enforced { get; set; }
    public int SignatureWindowSeconds { get; set; } = 300;
    public bool RequestSigningRequired { get; set; }

    /// <summary>Header carrying the per-device identifier. The `App` token
    /// is the product rename target; must match the mobile
    /// CtxSecurityConfig.</summary>
    public string DeviceIdHeader { get; set; } = "X-App-Device-Id";

    /// <summary>Header carrying `timestamp:signature`.</summary>
    public string SignatureHeader { get; set; } = "X-App-Signature";

    /// <summary>
    /// Paths that bypass signature verification even without endpoint
    /// metadata (fail-safe for registration/metadata bootstrap routes).
    /// </summary>
    public string[] SigningBypassPaths { get; set; } =
    [
        "/v1/security/app-instances",
        "/v1/security/metadata",
    ];
}

/// <summary>
/// Envelope-encryption configuration: versioned KEKs
/// (Security:Encryption:Keys:{version}:Key, Base64 32 bytes) + the
/// CurrentVersion pointer, and the dedicated blind-index HMAC key. Keys
/// come from environment secrets or a KMS — never from appsettings.json
/// in production. Nonces are NEVER configured: they are generated per
/// operation.
/// </summary>
public sealed class EncryptionOptions
{
    public const string SectionName = "Security:Encryption";

    public string CurrentVersion { get; set; } = string.Empty;
    public Dictionary<string, KeyEntry> Keys { get; set; } = [];

    /// <summary>HMAC-SHA256 key for blind indexes, distinct from every KEK.</summary>
    public string BlindIndexKey { get; set; } = string.Empty;

    public sealed class KeyEntry
    {
        public string Key { get; set; } = string.Empty;
    }
}

/// <summary>
/// Row-Level Security session configuration. The names are the `App`
/// rename targets on the database side; they must match the roles and
/// helper function created by the CtxRls migration helpers — the
/// RlsInterceptor and the policies are two halves of one contract.
/// </summary>
public sealed class RlsOptions
{
    public const string SectionName = "Security:Rls";

    /// <summary>Postgres setting read by get_current_user_id().</summary>
    public string SettingName { get; set; } = "app.current_user_id";

    /// <summary>NOLOGIN role background workers switch to for the
    /// internal_worker_bypass_* policies (SET LOCAL ROLE).</summary>
    public string WorkerRole { get; set; } = "app_internal_worker";
}

/// <summary>
/// The "Rbac" configuration section. Both maps are merged over the
/// consumer's seed defaults: an entry with an existing name replaces the
/// default; a new name defines a custom role or assignment.
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
/// The consumer-owned RBAC vocabulary the RoleCatalog validates against
/// and merges configuration over: default grant roles, default principal
/// assignments, and the set of every valid permission.
/// </summary>
public sealed class RoleCatalogSeed
{
    public required IReadOnlyDictionary<string, string[]> DefaultRoles { get; init; }
    public required IReadOnlyDictionary<string, string[]> DefaultAssignments { get; init; }
    public required IReadOnlySet<string> KnownPermissions { get; init; }
}
