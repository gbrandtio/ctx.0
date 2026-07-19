using CtxApp.Domain.Security;

namespace CtxApp.Domain.Notes;

/// <summary>
/// A user-owned note. <see cref="Title"/> and <see cref="Body"/> are
/// envelope-encrypted at rest; <see cref="TitleBlindIndex"/> allows exact-match
/// search over the encrypted title. Rows are isolated per user by RLS on
/// <see cref="UserId"/>.
/// </summary>
public sealed class Note
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required Guid UserId { get; init; }

    [Encrypted]
    public required string Title { get; set; }

    /// <summary>Deterministic blind index of the title, for searchable encrypted PII.</summary>
    public required string TitleBlindIndex { get; set; }

    [Encrypted]
    public required string Body { get; set; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
