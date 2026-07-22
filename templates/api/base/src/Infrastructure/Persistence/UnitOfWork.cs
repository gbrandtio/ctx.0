using CtxApp.Application.Abstractions;

namespace CtxApp.Infrastructure.Persistence;

public sealed class UnitOfWork(CtxAppDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
