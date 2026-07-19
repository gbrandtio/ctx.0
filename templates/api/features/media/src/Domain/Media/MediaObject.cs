using CtxApp.Domain.Security;

namespace CtxApp.Domain.Media;

/// <summary>
/// A user-owned stored file. <see cref="FileName"/> is envelope-encrypted at
/// rest; the bytes live outside the database under <see cref="StorageKey"/> in an
/// <c>IBlobStore</c> (also encrypted). Rows are isolated per user by RLS on
/// <see cref="UserId"/>.
/// </summary>
public sealed class MediaObject
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required Guid UserId { get; init; }

    [Encrypted]
    public required string FileName { get; set; }

    public required string ContentType { get; set; }

    public required long SizeBytes { get; set; }

    /// <summary>Opaque, server-generated key locating the encrypted blob in the store.</summary>
    public required string StorageKey { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
