namespace Application.Abstractions;

public sealed record GoogleUserInfo(string Subject, string Email, string? Name);

public interface IGoogleTokenValidator
{
    /// <summary>Validates a Google ID token against Google's public keys.</summary>
    Task<GoogleUserInfo> ValidateAsync(string idToken, CancellationToken ct);
}
