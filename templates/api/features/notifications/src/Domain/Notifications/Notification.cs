using CtxApp.Domain.Security;

namespace CtxApp.Domain.Notifications;

/// <summary>
/// A user-owned in-app notification. <see cref="Title"/> and <see cref="Body"/>
/// are envelope-encrypted at rest; rows are isolated per user by RLS on
/// <see cref="UserId"/>. <see cref="ReadAt"/> is null until the user marks it read.
/// </summary>
public sealed class Notification
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required Guid UserId { get; init; }

    [Encrypted]
    public required string Title { get; set; }

    [Encrypted]
    public required string Body { get; set; }

    public DateTimeOffset? ReadAt { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
