using CtxApp.Domain.Media;

namespace CtxApp.Application.Media;

public interface IMediaRepository
{
    void Add(MediaObject media);
    Task<List<MediaObject>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<MediaObject?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    void Remove(MediaObject media);
}
