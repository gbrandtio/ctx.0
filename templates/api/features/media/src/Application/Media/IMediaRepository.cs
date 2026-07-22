using CtxApp.Domain.Media;

namespace CtxApp.Application.Media;

public interface IMediaRepository
{
    void Add(MediaObject media);
    Task<List<MediaObject>> GetAllAsync(CancellationToken ct = default);
    Task<MediaObject?> GetByIdAsync(Guid id, CancellationToken ct = default);
    void Remove(MediaObject media);
}
