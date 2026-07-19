namespace Acme.Application.Security;

/// <summary>An access token plus the rotating refresh token issued alongside it.</summary>
public sealed record AuthTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);

/// <summary>The refresh-token lifetime, injected so it is configurable and testable.</summary>
public sealed record RefreshTokenTtl(TimeSpan Value);

/// <summary>Raised for any authentication failure; surfaced to callers as 401.</summary>
public sealed class AuthException(string message) : Exception(message);
