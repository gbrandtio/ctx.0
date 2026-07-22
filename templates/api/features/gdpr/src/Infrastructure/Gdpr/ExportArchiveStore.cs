using CtxApp.Application.Abstractions;
using CtxApp.Application.Gdpr;

namespace CtxApp.Infrastructure.Gdpr;

/// <summary>
/// Filesystem store for export archives, envelope-encrypted with the vendored
/// <see cref="IFieldCipher"/> exactly like the media feature's blob store — an
/// export bundle is the most concentrated personal data the system ever writes,
/// so it never touches disk in the clear. This deliberately does not reuse
/// <c>IBlobStore</c>: the privacy feature has to work with <c>media</c> disabled.
/// Storage keys are validated as 32-char hex (the ids the endpoints mint) so a
/// key can never escape the configured root.
/// </summary>
public sealed class ExportArchiveStore : IExportArchiveStore
{
    private readonly string _root;
    private readonly IFieldCipher _cipher;

    public ExportArchiveStore(GdprOptions options, IFieldCipher cipher)
    {
        _root = Path.GetFullPath(options.ExportRoot);
        _cipher = cipher;
        Directory.CreateDirectory(_root);
    }

    public async Task WriteAsync(string key, byte[] content, CancellationToken cancellationToken = default)
    {
        var envelope = _cipher.Encrypt(Convert.ToBase64String(content));
        await File.WriteAllTextAsync(PathFor(key), envelope, cancellationToken);
    }

    public async Task<byte[]> ReadAsync(string key, CancellationToken cancellationToken = default)
    {
        var envelope = await File.ReadAllTextAsync(PathFor(key), cancellationToken);
        return Convert.FromBase64String(_cipher.Decrypt(envelope));
    }

    /// <summary>Remove the archive at <paramref name="key"/> if it exists; a no-op otherwise.</summary>
    public void Delete(string key)
    {
        var path = PathFor(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public bool Exists(string key) => File.Exists(PathFor(key));

    private string PathFor(string key)
    {
        if (key.Length != 32 || !key.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("Invalid storage key.", nameof(key));
        }
        return Path.Combine(_root, key);
    }
}
