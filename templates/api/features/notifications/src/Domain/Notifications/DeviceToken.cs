using CtxApp.Domain.Security;

namespace CtxApp.Domain.Notifications;

/// <summary>
/// A registered push-delivery target for a signed-in user. The FCM
/// <see cref="Token"/> is envelope-encrypted at rest; <see cref="TokenBlindIndex"/>
/// is its deterministic HMAC, used to dedupe/look up a registration without
/// exposing the token. Rows are isolated per user by RLS on <see cref="UserId"/>.
/// </summary>
public sealed class DeviceToken
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required Guid UserId { get; init; }

    /// <summary>Delivery platform: "android", "ios", or "web".</summary>
    public required string Platform { get; set; }

    [Encrypted]
    public required string Token { get; set; }

    /// <summary>Deterministic blind index of the token, for dedupe/lookup.</summary>
    public required string TokenBlindIndex { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
