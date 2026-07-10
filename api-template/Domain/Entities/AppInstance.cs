namespace Domain.Entities;

/// <summary>
/// A registered app installation: device id + ECDSA P-256 public key used
/// by RequestSigningMiddleware (APPLICATION_LAYER_SECURITY.md §2).
/// </summary>
public class AppInstance
{
    public long Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>Base64 SubjectPublicKeyInfo or raw uncompressed point.</summary>
    public string PublicKey { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
