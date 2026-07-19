using CtxApp.Application.Abstractions;
using CtxApp.Domain.Security;
using Microsoft.EntityFrameworkCore;

namespace CtxApp.Infrastructure.Persistence;

/// <summary>EF Core-backed refresh token store over the app's <see cref="CtxAppDbContext"/>.</summary>
public sealed class EfRefreshTokenStore(CtxAppDbContext db) : IRefreshTokenStore
{
    public async Task AddAsync(RefreshToken token, CancellationToken ct = default)
        => await db.Set<RefreshToken>().AddAsync(token, ct);

    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct = default)
        => db.Set<RefreshToken>().FirstOrDefaultAsync(x => x.TokenHash == tokenHash, ct);

    public async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset revokedAt, CancellationToken ct = default)
    {
        var active = await db.Set<RefreshToken>()
            .Where(x => x.FamilyId == familyId && x.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var token in active)
        {
            token.RevokedAt = revokedAt;
        }
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
