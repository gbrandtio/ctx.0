namespace Domain.Entities;

/// <summary>
/// Pre-registration email verification code
/// (AUTHENTICATION.md — POST /v1/users/register/send-code). Codes are
/// stored hashed; attempts are capped to block brute force.
/// </summary>
public class SignupVerification
{
    public const int MaxAttempts = 5;

    public long Id { get; set; }
    public string EmailHash { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public int Attempts { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
