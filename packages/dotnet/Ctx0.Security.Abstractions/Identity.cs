namespace Ctx0.Security.Abstractions;

/// <summary>Claims baked into an access token.</summary>
public sealed record AccessTokenSubject(
    long UserId,
    string Username,
    string Role,
    string? Type = null,
    IReadOnlyList<long>? OrgIds = null,
    long? ProjectId = null);

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAtUtc) CreateAccessToken(AccessTokenSubject subject);

    /// <summary>64 random bytes, Base64 — the opaque refresh token.</summary>
    string GenerateRefreshToken();

    /// <summary>SHA-256 hex of a refresh token; only hashes are persisted.</summary>
    string HashRefreshToken(string refreshToken);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);

    /// <summary>
    /// Performs a bcrypt verification against a dummy hash so "user not
    /// found" takes the same time as "wrong password" (constant-time
    /// behaviour).
    /// </summary>
    void DummyVerify();
}

/// <summary>
/// HMAC-SHA256 blind index for searchable encrypted PII. Deterministic
/// per input, keyed with a dedicated index key distinct from the KEK.
/// </summary>
public interface IBlindIndexProvider
{
    string ComputeHash(string value);
}

public sealed record GoogleUserInfo(string Subject, string Email, string? Name);

public interface IGoogleTokenValidator
{
    /// <summary>Validates a Google ID token against Google's public keys.</summary>
    Task<GoogleUserInfo> ValidateAsync(string idToken, CancellationToken ct);
}

/// <summary>Time-based snowflake IDs for bigint primary keys.</summary>
public interface IIdGenerator
{
    long NextId();
}

/// <summary>
/// Ambient identity for the RLS layer. The RlsInterceptor reads UserId to
/// set the session variable transaction-locally; background workers
/// activate the system bypass to run as the internal worker role.
/// </summary>
public interface ICurrentUserProvider
{
    long? UserId { get; }
    bool IsSystemBypassActive { get; }
}

/// <summary>
/// Lookup of a registered device's signing public key (Base64 SPKI or raw
/// uncompressed point) for RequestSigningMiddleware. The consumer app
/// implements it over its device-registration store.
/// </summary>
public interface IDeviceKeyStore
{
    Task<string?> FindPublicKeyAsync(string deviceId, CancellationToken ct);
}

/// <summary>
/// Injectable time source: application logic never calls DateTime.UtcNow
/// directly so tests can time-travel. Always UTC.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}

/// <summary>
/// Thrown by security-plane components when a credential fails
/// validation. The host's exception handler must map it to 401 with a
/// client-safe message (the template's GlobalExceptionHandler does).
/// </summary>
public sealed class CtxAuthenticationException(string message) : Exception(message);
