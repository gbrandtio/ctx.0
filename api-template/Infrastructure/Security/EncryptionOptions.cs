namespace Infrastructure.Security;

/// <summary>
/// Envelope-encryption configuration (ENVELOPE_ENCRYPTION_ARCHITECTURE.md
/// §4): versioned KEKs (Security:Encryption:Keys:{version}:Key, Base64
/// 32 bytes) + the CurrentVersion pointer, and the dedicated blind-index
/// HMAC key. Keys come from environment secrets or a KMS — never from
/// appsettings.json in production. Nonces are NEVER configured: they are
/// generated per operation.
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
