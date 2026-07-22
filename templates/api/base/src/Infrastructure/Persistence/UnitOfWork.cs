using CtxApp.Application.Abstractions;

namespace CtxApp.Infrastructure.Persistence;

public sealed class UnitOfWork(CtxAppDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);
}
