using CtxApp.Application.Gdpr;
using CtxApp.Domain.Auth;
using CtxApp.Domain.Gdpr;
using Microsoft.EntityFrameworkCore;

namespace CtxApp.Infrastructure.Persistence;

public sealed class PrivacyRepository(CtxAppDbContext dbContext) : IPrivacyRepository
{
    public Task<ConsentRecord?> GetLatestConsentAsync(Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.Set<ConsentRecord>()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.DecidedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public void AddConsent(ConsentRecord record) => dbContext.Set<ConsentRecord>().Add(record);

    public Task<DataExportJob?> GetExportJobAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Set<DataExportJob>().FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

    public void AddExportJob(DataExportJob job) => dbContext.Set<DataExportJob>().Add(job);

    public Task<List<DataExportJob>> GetStaleExportJobsAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        dbContext.Set<DataExportJob>()
            .Where(j => j.UserId == userId && j.DownloadedAt == null && j.ExpiresAt != null && j.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

    public Task<UserCredential?> GetUserCredentialAsync(Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.Set<UserCredential>().AsNoTracking().FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);
}
