namespace CtxApp.Domain.Auth;

/// <summary>Local email/password credential for a user (one-to-one with the user).</summary>
public sealed class UserCredential
{
    public required Guid UserId { get; init; }

    /// <summary>PBKDF2 password hash (<c>iterations.salt.hash</c>); the raw password is never stored.</summary>
    public required string PasswordHash { get; set; }
}
