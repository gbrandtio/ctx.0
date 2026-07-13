
namespace Domain.Entities;

/// <summary>
/// Consumer user. PII properties (Username, Email, Name) are envelope-
/// encrypted at rest via the EnvelopeEncryptionInterceptor; the *_hash
/// companions are HMAC blind indexes for lookups
/// (ENVELOPE_ENCRYPTION_ARCHITECTURE.md).
/// </summary>
public class User
{
    public long Id { get; set; }

    // ---- PII (encrypted at rest) ----
    [CtxEncrypted] public string Username { get; set; } = string.Empty;
    [CtxEncrypted] public string Email { get; set; } = string.Empty;
    [CtxEncrypted] public string? Name { get; set; }

    // ---- Blind indexes ----
    public string UsernameHash { get; set; } = string.Empty;
    public string EmailHash { get; set; } = string.Empty;

    /// <summary>BCrypt hash; null for Google-only accounts.</summary>
    public string? PasswordHash { get; set; }

    /// <summary>KEK-wrapped per-row DEK, prefixed with the KEK version (e.g. "v1:").</summary>
    public string EncryptedDek { get; set; } = string.Empty;

    /// <summary>GDPR delete anonymizes instead of hard-deleting (ledger integrity).</summary>
    public bool IsAnonymized { get; set; }

    /// <summary>Whether the user has granted consent for tracking and analytics.</summary>
    public bool HasTrackingConsent { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public UserGoogleIdentity? GoogleIdentity { get; set; }
    public UserFirebaseIdentity? FirebaseIdentity { get; set; }
    public UserTotals? Totals { get; set; }
}
