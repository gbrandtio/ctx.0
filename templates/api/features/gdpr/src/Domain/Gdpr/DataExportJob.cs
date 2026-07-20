namespace CtxApp.Domain.Gdpr;

/// <summary>Lifecycle of a data-export request.</summary>
public enum DataExportStatus
{
    Pending,
    Ready,
    Failed,
    Expired,
}

/// <summary>
/// A user's request for a copy of their data. The archive itself lives outside
/// the database under <see cref="StorageKey"/>, encrypted at rest; the row keeps
/// only the hash of the single-use download token, so a leaked database gives no
/// one access to the bundle. Rows are isolated per user by RLS on
/// <see cref="UserId"/>.
/// </summary>
public sealed class DataExportJob
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required Guid UserId { get; init; }

    public DataExportStatus Status { get; set; } = DataExportStatus.Pending;

    /// <summary>Opaque, server-generated key locating the encrypted archive in the store.</summary>
    public required string StorageKey { get; init; }

    /// <summary>Hash of the one-time download token; the token itself is shown to the user once.</summary>
    public required string DownloadTokenHash { get; init; }

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>When the archive stops being downloadable and is purged.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>Set the moment the archive is handed over; a bundle is downloadable once.</summary>
    public DateTimeOffset? DownloadedAt { get; set; }

    public long SizeBytes { get; set; }

    /// <summary>Why the export failed, when <see cref="Status"/> is <see cref="DataExportStatus.Failed"/>.</summary>
    public string? Error { get; set; }
}
