using CtxApp.Application.Profile;
using CtxApp.Domain.Profile;
using Microsoft.EntityFrameworkCore;

namespace CtxApp.Infrastructure.Persistence;

public sealed class ProfileRepository(CtxAppDbContext db) : IProfileRepository
{
    public Task<UserProfile?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        db.Set<UserProfile>().FirstOrDefaultAsync(p => p.UserId == userId, ct);

    public void Add(UserProfile profile) => db.Set<UserProfile>().Add(profile);
}
