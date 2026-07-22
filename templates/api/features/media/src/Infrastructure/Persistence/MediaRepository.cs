using CtxApp.Application.Media;
using CtxApp.Domain.Media;
using Microsoft.EntityFrameworkCore;

namespace CtxApp.Infrastructure.Persistence;

public sealed class MediaRepository(CtxAppDbContext db) : IMediaRepository
{
    public void Add(MediaObject media) => db.Set<MediaObject>().Add(media);

    public Task<List<MediaObject>> GetAllAsync(CancellationToken ct = default) =>
        db.Set<MediaObject>().OrderByDescending(m => m.CreatedAt).ToListAsync(ct);

    public Task<MediaObject?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        db.Set<MediaObject>().FirstOrDefaultAsync(m => m.Id == id, ct);

    public void Remove(MediaObject media) => db.Set<MediaObject>().Remove(media);
}
