using CtxApp.Domain.Media;

namespace CtxApp.Application.Media;

public sealed record MediaDto(Guid Id, string FileName, string ContentType, long SizeBytes, DateTimeOffset CreatedAt);

public interface IMediaService
{
    Task<MediaDto> CreateMediaAsync(Guid userId, string fileName, string contentType, long sizeBytes, string storageKey, CancellationToken ct = default);
    Task<List<MediaDto>> GetAllAsync(CancellationToken ct = default);
    Task<MediaObject?> GetMediaObjectAsync(Guid id, CancellationToken ct = default);
    Task DeleteMediaAsync(MediaObject media, CancellationToken ct = default);
}
