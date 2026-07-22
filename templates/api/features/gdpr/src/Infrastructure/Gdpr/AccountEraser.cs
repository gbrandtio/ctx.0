using CtxApp.Application.Abstractions;
using CtxApp.Application.Gdpr;
using CtxApp.Domain.Gdpr;
using CtxApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CtxApp.Infrastructure.Gdpr;

/// <summary>
/// Erases an account: every registered <see cref="IPersonalDataContributor"/>
/// drops what its feature holds, this feature drops the consent trail and any
/// export archives, and the user row goes last — all inside one transaction, so
/// a failure anywhere leaves the account intact rather than half-deleted. Deleting
/// the refresh tokens (the auth contributor's job) is what kills live sessions;
/// an already-issued access token stops working as soon as the user row is gone.
/// </summary>
public sealed class AccountEraser(
    CtxAppDbContext dbContext,
    IEnumerable<IPersonalDataContributor> contributors,
    IExportArchiveStore archives)
{
    public async Task EraseAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        foreach (var contributor in contributors)
        {
            await contributor.EraseAsync(userId, cancellationToken);
        }

        var jobs = await dbContext.Set<DataExportJob>().Where(j => j.UserId == userId).ToListAsync(cancellationToken);
        foreach (var job in jobs)
        {
            archives.Delete(job.StorageKey);
        }
        dbContext.Set<DataExportJob>().RemoveRange(jobs);

        var consents = await dbContext.Set<ConsentRecord>().Where(c => c.UserId == userId).ToListAsync(cancellationToken);
        dbContext.Set<ConsentRecord>().RemoveRange(consents);

        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is not null)
        {
            dbContext.Users.Remove(user);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
