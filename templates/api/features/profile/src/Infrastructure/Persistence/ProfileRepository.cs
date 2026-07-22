using CtxApp.Application.Profile;
using CtxApp.Domain.Profile;
using Microsoft.EntityFrameworkCore;

namespace CtxApp.Infrastructure.Persistence;

public sealed class ProfileRepository(CtxAppDbContext dbContext) : IProfileRepository
{
    public Task<UserProfile?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.Set<UserProfile>().FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);

    public void Add(UserProfile profile) => dbContext.Set<UserProfile>().Add(profile);
}
