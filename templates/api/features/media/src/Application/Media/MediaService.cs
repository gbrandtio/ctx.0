using CtxApp.Application.Abstractions;
using CtxApp.Domain.Media;

namespace CtxApp.Application.Media;

public sealed class MediaService(IMediaRepository mediaRepository, IUnitOfWork unitOfWork) : IMediaService
{
    public async Task<MediaDto> CreateMediaAsync(Guid userId, string fileName, string contentType, long sizeBytes, string storageKey, CancellationToken ct = default)
    {
        var media = new MediaObject
        {
            UserId = userId,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            StorageKey = storageKey,
        };
        mediaRepository.Add(media);
        await unitOfWork.SaveChangesAsync(ct);

        return new MediaDto(media.Id, media.FileName, media.ContentType, media.SizeBytes, media.CreatedAt);
    }

    public async Task<List<MediaDto>> GetAllAsync(CancellationToken ct = default)
    {
        var items = await mediaRepository.GetAllAsync(ct);
        return items.Select(m => new MediaDto(m.Id, m.FileName, m.ContentType, m.SizeBytes, m.CreatedAt)).ToList();
    }

    public Task<MediaObject?> GetMediaObjectAsync(Guid id, CancellationToken ct = default) =>
        mediaRepository.GetByIdAsync(id, ct);

    public async Task DeleteMediaAsync(MediaObject media, CancellationToken ct = default)
    {
        mediaRepository.Remove(media);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
