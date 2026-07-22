using CtxApp.Application.Media;
using CtxApp.Domain.Media;
using Microsoft.EntityFrameworkCore;

namespace CtxApp.Infrastructure.Persistence;

public sealed class MediaRepository(CtxAppDbContext dbContext) : IMediaRepository
{
    public void Add(MediaObject media) => dbContext.Set<MediaObject>().Add(media);

    public Task<List<MediaObject>> GetAllAsync(CancellationToken cancellationToken = default) =>
        dbContext.Set<MediaObject>().OrderByDescending(m => m.CreatedAt).ToListAsync(cancellationToken);

    public Task<MediaObject?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Set<MediaObject>().FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public void Remove(MediaObject media) => dbContext.Set<MediaObject>().Remove(media);
}
