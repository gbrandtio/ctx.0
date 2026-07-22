using System.Runtime.CompilerServices;
using CtxApp.Application.Abstractions;
using CtxApp.Application.Media;
using CtxApp.Domain.Media;
using CtxApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CtxApp.Infrastructure.Gdpr;

/// <summary>
/// The media feature's personal data. The export carries each file's metadata as
/// JSON *and* the file itself as an archive attachment, since the bytes are the
/// user's own content. Erasure deletes the blobs from the store before dropping
/// the metadata rows, so nothing is left orphaned on disk.
/// </summary>
public sealed class MediaPersonalData(CtxAppDbContext dbContext, IBlobStore blobStore)
    : IPersonalDataContributor, IPersonalDataAttachments
{
    public string Section => "media";

    public async Task<object?> ExportAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var objects = await OwnedAsync(userId, cancellationToken);
        return objects.Count == 0
            ? null
            : objects
                .Select(m => new
                {
                    m.Id,
                    m.FileName,
                    m.ContentType,
                    m.SizeBytes,
                    m.CreatedAt,
                    File = ArchivePath(m),
                })
                .ToList();
    }

    public async IAsyncEnumerable<PersonalDataAttachment> AttachmentsAsync(
        Guid userId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        foreach (var media in await OwnedAsync(userId, cancellationToken))
        {
            var key = media.StorageKey;
            yield return new PersonalDataAttachment(ArchivePath(media), token => blobStore.ReadAsync(key, token));
        }
    }

    public async Task EraseAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var objects = await dbContext.Set<MediaObject>().Where(m => m.UserId == userId).ToListAsync(cancellationToken);
        foreach (var media in objects)
        {
            await blobStore.DeleteAsync(media.StorageKey, cancellationToken);
        }
        dbContext.Set<MediaObject>().RemoveRange(objects);
    }

    private Task<List<MediaObject>> OwnedAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Set<MediaObject>()
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Where the blob lands inside the archive. The id keeps entries unique when
    /// two uploads share a name, and the file name is reduced to its safe
    /// characters so a stored name can never escape the media/ folder.
    /// </summary>
    private static string ArchivePath(MediaObject media)
    {
        var name = new string(media.FileName.Select(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' ? c : '_').ToArray());
        return $"media/{media.Id:n}-{(name.Length == 0 ? "file" : name)}";
    }
}
