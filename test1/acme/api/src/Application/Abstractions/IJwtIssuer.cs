namespace Acme.Application.Abstractions;

/// <summary>Issues short-lived JWT access tokens for an authenticated user.</summary>
public interface IJwtIssuer
{
    /// <summary>Issue a signed access token whose subject is <paramref name="userId"/>.</summary>
    (string Token, DateTimeOffset ExpiresAt) Issue(Guid userId);
}
