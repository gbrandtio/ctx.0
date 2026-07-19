using CtxApp.Application.Abstractions;
using CtxApp.Application.Media;

namespace CtxApp.Infrastructure.Media;

/// <summary>
/// Filesystem-backed <see cref="IBlobStore"/> for local and single-instance use.
/// Blobs are envelope-encrypted with the vendored <see cref="IFieldCipher"/>
/// before hitting disk, so files at rest are ciphertext in the same format as
/// encrypted columns. Storage keys are validated as 32-char hex (the ids the
/// endpoints generate) so a key can never escape the configured root.
/// </summary>
public sealed class LocalBlobStore : IBlobStore
{
    private readonly string _root;
    private readonly IFieldCipher _cipher;

    public LocalBlobStore(MediaOptions options, IFieldCipher cipher)
    {
        _root = Path.GetFullPath(options.Root);
        _cipher = cipher;
        Directory.CreateDirectory(_root);
    }

    public async Task WriteAsync(string key, Stream content, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var envelope = _cipher.Encrypt(Convert.ToBase64String(buffer.ToArray()));
        await File.WriteAllTextAsync(PathFor(key), envelope, ct);
    }

    public async Task<Stream> ReadAsync(string key, CancellationToken ct = default)
    {
        var envelope = await File.ReadAllTextAsync(PathFor(key), ct);
        var bytes = Convert.FromBase64String(_cipher.Decrypt(envelope));
        return new MemoryStream(bytes);
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var path = PathFor(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
        return Task.CompletedTask;
    }

    private string PathFor(string key)
    {
        // Keys are server-minted 32-char hex GUIDs; reject anything else so a key
        // can never contain a path separator or traversal segment.
        if (key.Length != 32 || !key.All(Uri.IsHexDigit))
        {
            throw new ArgumentException("Invalid storage key.", nameof(key));
        }
        return Path.Combine(_root, key);
    }
}
