namespace Contracts.Auth;

// Request DTOs never carry a UserId — identity comes exclusively from JWT
// claims (ARCHITECTURE_OVERVIEW.md, Contracts layer).

public sealed record SendSignupCodeRequest(string Email);

public sealed record RegisterUserRequest(
    string Username,
    string Email,
    string Password,
    string VerificationCode,
    string? Name,
    Dictionary<string, bool>? Consents);

public sealed record AuthenticateRequest(string UsernameOrEmail, string Password);

public sealed record GoogleAuthenticateRequest(string IdToken);

public sealed record RefreshRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>Shape defined by AUTHENTICATION.md — flat, both tokens included.</summary>
public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAtUtc,
    long UserId,
    string Username,
    string Email);
