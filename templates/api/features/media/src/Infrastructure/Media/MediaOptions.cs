using Microsoft.Extensions.Configuration;

namespace CtxApp.Infrastructure.Media;

/// <summary>
/// Configuration for the media feature, read from the <c>MEDIA_*</c> environment variables.
/// </summary>
public sealed class MediaOptions
{
    /// <summary>The environment-variable prefix for each allowlisted content type (suffix is the index).</summary>
    public const string AllowedContentTypesPrefix = "MEDIA_ALLOWED_CONTENT_TYPES_";

    /// <summary>Filesystem directory holding the encrypted blobs.</summary>
    public string Root { get; set; } = "./_media";

    /// <summary>Maximum accepted upload size in bytes (default 10 MiB).</summary>
    public long MaxBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Optional content-type allowlist. Empty means every content type is accepted.
    /// </summary>
    public List<string> AllowedContentTypes { get; set; } = new();

    /// <summary>
    /// Read the options from the <c>MEDIA_*</c> environment variables. Each
    /// <c>MEDIA_ALLOWED_CONTENT_TYPES_&lt;index&gt;</c> variable contributes one allowlist entry,
    /// applied in ascending numeric index order.
    /// </summary>
    public static MediaOptions FromConfiguration(IConfiguration configuration)
    {
        var defaults = new MediaOptions();
        var allowed = configuration.AsEnumerable()
            .Where(pair => pair.Value is not null && pair.Key.StartsWith(AllowedContentTypesPrefix, StringComparison.Ordinal))
            .OrderBy(pair => int.TryParse(pair.Key[AllowedContentTypesPrefix.Length..], out var index) ? index : int.MaxValue)
            .Select(pair => pair.Value!)
            .ToList();

        return new MediaOptions
        {
            Root = configuration["MEDIA_ROOT"] ?? defaults.Root,
            MaxBytes = long.TryParse(configuration["MEDIA_MAX_BYTES"], out var maxBytes) ? maxBytes : defaults.MaxBytes,
            AllowedContentTypes = allowed,
        };
    }

    public bool IsAllowed(string contentType) =>
        AllowedContentTypes.Count == 0
        || AllowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase);
}
