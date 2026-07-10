namespace Domain.Entities;

/// <summary>
/// FCM token registration (NOTIFICATIONS.md §2). Tokens are PII:
/// encrypted at rest, decrypted only in worker memory during dispatch.
/// </summary>
public class UserFirebaseIdentity
{
    public long Id { get; set; }
    public long UserId { get; set; }

    /// <summary>Encrypted FCM token (envelope encryption, user's DEK).</summary>
    public string Token { get; set; } = string.Empty;

    public string EncryptedDek { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
