using CtxApp.Domain.Security;

namespace CtxApp.Domain.Profile;

/// <summary>
/// A user's editable account profile, one-to-one with the user (keyed by
/// <see cref="UserId"/>, like the auth credential). <see cref="DisplayName"/> and
/// <see cref="Bio"/> are envelope-encrypted at rest; the row is isolated per user
/// by RLS on <see cref="UserId"/>. The avatar is either a plain
/// <see cref="AvatarUrl"/> or, when the <c>media</c> feature is enabled, an
/// <see cref="AvatarMediaId"/> pointing at an uploaded media object.
/// </summary>
public sealed class UserProfile
{
    public required Guid UserId { get; init; }

    [Encrypted]
    public required string DisplayName { get; set; }

    [Encrypted]
    public string? Bio { get; set; }

    public string? AvatarUrl { get; set; }

    public Guid? AvatarMediaId { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
