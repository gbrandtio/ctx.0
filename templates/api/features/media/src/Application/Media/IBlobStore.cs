namespace CtxApp.Application.Media;

/// <summary>
/// Stores opaque binary blobs by key, outside the relational database. The
/// vendored <c>LocalBlobStore</c> encrypts blobs at rest with the same envelope
/// cipher used for encrypted columns; swap in an S3-compatible implementation
/// without touching the endpoints. Keys are server-generated, never client input.
/// </summary>
public interface IBlobStore
{
    /// <summary>Write (or overwrite) the blob at <paramref name="key"/> from <paramref name="content"/>.</summary>
    Task WriteAsync(string key, Stream content, CancellationToken ct = default);

    /// <summary>Open the blob at <paramref name="key"/> as a readable, seekable-from-start stream.</summary>
    Task<Stream> ReadAsync(string key, CancellationToken ct = default);

    /// <summary>Remove the blob at <paramref name="key"/> if it exists; a no-op otherwise.</summary>
    Task DeleteAsync(string key, CancellationToken ct = default);
}
