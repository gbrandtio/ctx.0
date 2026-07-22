using CtxApp.Application.Abstractions;
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
    CtxAppDbContext db,
    IEnumerable<IPersonalDataContributor> contributors,
    IExportArchiveStore archives)
{
    public async Task EraseAsync(Guid userId, CancellationToken ct = default)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        foreach (var contributor in contributors)
        {
            await contributor.EraseAsync(userId, ct);
        }

        var jobs = await db.Set<DataExportJob>().Where(j => j.UserId == userId).ToListAsync(ct);
        foreach (var job in jobs)
        {
            archives.Delete(job.StorageKey);
        }
        db.Set<DataExportJob>().RemoveRange(jobs);

        var consents = await db.Set<ConsentRecord>().Where(c => c.UserId == userId).ToListAsync(ct);
        db.Set<ConsentRecord>().RemoveRange(consents);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is not null)
        {
            db.Users.Remove(user);
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }
}
