namespace CtxApp.Infrastructure.Media;

/// <summary>
/// Configuration for the media feature, bound from the <c>Media</c> section.
/// </summary>
public sealed class MediaOptions
{
    public const string Section = "Media";

    /// <summary>Filesystem directory holding the encrypted blobs.</summary>
    public string Root { get; set; } = "./_media";

    /// <summary>Maximum accepted upload size in bytes (default 10 MiB).</summary>
    public long MaxBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Optional content-type allowlist. Empty means every content type is accepted.
    /// </summary>
    public List<string> AllowedContentTypes { get; set; } = new();

    public bool IsAllowed(string contentType) =>
        AllowedContentTypes.Count == 0
        || AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase);
}
