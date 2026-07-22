using CtxApp.Application.Abstractions;
using CtxApp.Domain.Gdpr;

namespace CtxApp.Application.Gdpr;

public sealed class PrivacyService(
    IPrivacyRepository repository,
    IUnitOfWork unitOfWork,
    IExportJobQueue queue,
    IExportArchiveStore archives,
    ITokenGenerator tokens,
    ITokenHasher hasher,
    IClock clock,
    IPasswordHasher passwords) : IPrivacyService
{
    public async Task<ConsentDto?> GetLatestConsentAsync(Guid userId, CancellationToken ct = default)
    {
        var record = await repository.GetLatestConsentAsync(userId, ct);
        return record is null ? null : new ConsentDto(record.PolicyVersion, record.Purposes.Split(',', StringSplitOptions.RemoveEmptyEntries), record.Source, record.DecidedAt);
    }

    public async Task<ConsentDto> RecordConsentAsync(Guid userId, string policyVersion, string[] purposes, string? source, CancellationToken ct = default)
    {
        var record = new ConsentRecord
        {
            UserId = userId,
            PolicyVersion = policyVersion,
            Purposes = string.Join(',', purposes.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct()),
            Source = string.IsNullOrWhiteSpace(source) ? "app" : source,
        };
        repository.AddConsent(record);
        await unitOfWork.SaveChangesAsync(ct);

        return new ConsentDto(record.PolicyVersion, record.Purposes.Split(',', StringSplitOptions.RemoveEmptyEntries), record.Source, record.DecidedAt);
    }

    public async Task<ExportJobCreatedDto> RequestExportAsync(Guid userId, CancellationToken ct = default)
    {
        await PurgeExpiredAsync(userId, ct);

        var token = tokens.NewToken();
        var job = new DataExportJob
        {
            UserId = userId,
            StorageKey = Guid.NewGuid().ToString("n"),
            DownloadTokenHash = hasher.Hash(token),
        };
        repository.AddExportJob(job);
        await unitOfWork.SaveChangesAsync(ct);

        await queue.EnqueueAsync(new ExportTicket(job.Id, userId), ct);

        return new ExportJobCreatedDto(job.Id, job.Status.ToString(), token);
    }

    public async Task<ExportJobDto?> GetExportJobAsync(Guid id, CancellationToken ct = default)
    {
        var job = await repository.GetExportJobAsync(id, ct);
        if (job is null)
        {
            return null;
        }

        return new ExportJobDto(job.Id, job.Status.ToString(), job.CreatedAt, job.CompletedAt, job.ExpiresAt, job.DownloadedAt, job.SizeBytes, job.Error);
    }

    public async Task<(byte[] Bundle, string ContentType, string FileName)?> DownloadExportAsync(Guid id, string token, CancellationToken ct = default)
    {
        var job = await repository.GetExportJobAsync(id, ct);
        if (job is null)
        {
            return null;
        }

        if (string.IsNullOrEmpty(token) || !FixedTimeEquals(hasher.Hash(token), job.DownloadTokenHash))
        {
            throw new UnauthorizedAccessException("Invalid download token");
        }

        if (job.Status != DataExportStatus.Ready)
        {
            throw new InvalidOperationException($"Export is not ready: {job.Status}");
        }

        if (job.DownloadedAt is not null || job.ExpiresAt <= clock.UtcNow)
        {
            if (job.DownloadedAt is null)
            {
                await ExpireAsync(job, ct);
            }
            throw new InvalidOperationException("Export has been consumed or has expired");
        }

        var bundle = await archives.ReadAsync(job.StorageKey, ct);

        job.DownloadedAt = clock.UtcNow;
        archives.Delete(job.StorageKey);
        await unitOfWork.SaveChangesAsync(ct);

        return (bundle, "application/zip", $"ctx-export-{job.Id:n}.zip");
    }

    public async Task<bool> VerifyPasswordAsync(Guid userId, string password, CancellationToken ct = default)
    {
        var credential = await repository.GetUserCredentialAsync(userId, ct);
        if (credential is null) return false;

        return passwords.Verify(password, credential.PasswordHash);
    }

    private async Task PurgeExpiredAsync(Guid userId, CancellationToken ct)
    {
        var stale = await repository.GetStaleExportJobsAsync(userId, clock.UtcNow, ct);
        foreach (var job in stale)
        {
            archives.Delete(job.StorageKey);
            job.Status = DataExportStatus.Expired;
        }
        if (stale.Count > 0)
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
    }

    private async Task ExpireAsync(DataExportJob job, CancellationToken ct)
    {
        archives.Delete(job.StorageKey);
        job.Status = DataExportStatus.Expired;
        await unitOfWork.SaveChangesAsync(ct);
    }

    private static bool FixedTimeEquals(string a, string b) =>
        System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(a), System.Text.Encoding.UTF8.GetBytes(b));
}
