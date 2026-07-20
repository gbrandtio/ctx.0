namespace CtxApp.Domain.Gdpr;

/// <summary>
/// One consent decision by a user, against one version of the privacy notice.
/// The table is append-only — a withdrawal is a new row with fewer purposes — so
/// it stands as the audit trail the regulation expects; the newest row for a user
/// is their current position. Rows are isolated per user by RLS on
/// <see cref="UserId"/>.
/// </summary>
public sealed class ConsentRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required Guid UserId { get; init; }

    /// <summary>The privacy-notice version the user was shown, e.g. "2024-11-01".</summary>
    public required string PolicyVersion { get; init; }

    /// <summary>
    /// Comma-separated ids of the optional purposes the user accepted (e.g.
    /// "analytics,marketing"). Empty means essential processing only.
    /// </summary>
    public required string Purposes { get; init; }

    /// <summary>Where the decision was made: "app" or "web".</summary>
    public required string Source { get; init; }

    public DateTimeOffset DecidedAt { get; init; } = DateTimeOffset.UtcNow;
}
