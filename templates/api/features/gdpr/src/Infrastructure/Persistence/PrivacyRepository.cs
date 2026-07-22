using CtxApp.Application.Gdpr;
using CtxApp.Domain.Auth;
using CtxApp.Domain.Gdpr;
using Microsoft.EntityFrameworkCore;

namespace CtxApp.Infrastructure.Persistence;

public sealed class PrivacyRepository(CtxAppDbContext db) : IPrivacyRepository
{
    public Task<ConsentRecord?> GetLatestConsentAsync(Guid userId, CancellationToken ct = default) =>
        db.Set<ConsentRecord>()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.DecidedAt)
            .FirstOrDefaultAsync(ct);

    public void AddConsent(ConsentRecord record) => db.Set<ConsentRecord>().Add(record);

    public Task<DataExportJob?> GetExportJobAsync(Guid id, CancellationToken ct = default) =>
        db.Set<DataExportJob>().FirstOrDefaultAsync(j => j.Id == id, ct);

    public void AddExportJob(DataExportJob job) => db.Set<DataExportJob>().Add(job);

    public Task<List<DataExportJob>> GetStaleExportJobsAsync(Guid userId, DateTimeOffset now, CancellationToken ct = default) =>
        db.Set<DataExportJob>()
            .Where(j => j.UserId == userId && j.DownloadedAt == null && j.ExpiresAt != null && j.ExpiresAt <= now)
            .ToListAsync(ct);

    public Task<UserCredential?> GetUserCredentialAsync(Guid userId, CancellationToken ct = default) =>
        db.Set<UserCredential>().AsNoTracking().FirstOrDefaultAsync(c => c.UserId == userId, ct);
}
