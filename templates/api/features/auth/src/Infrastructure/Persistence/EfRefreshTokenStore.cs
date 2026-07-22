using CtxApp.Application.Abstractions;
using CtxApp.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace CtxApp.Infrastructure.Persistence;

/// <summary>EF Core-backed refresh token store over the app's <see cref="CtxAppDbContext"/>.</summary>
public sealed class EfRefreshTokenStore(CtxAppDbContext dbContext) : IRefreshTokenStore
{
    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default)
        => await dbContext.Set<RefreshToken>().AddAsync(token, cancellationToken);

    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        => dbContext.Set<RefreshToken>().FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

    public async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset revokedAt, CancellationToken cancellationToken = default)
    {
        var active = await dbContext.Set<RefreshToken>()
            .Where(x => x.FamilyId == familyId && x.RevokedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var token in active)
        {
            token.RevokedAt = revokedAt;
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);
}
