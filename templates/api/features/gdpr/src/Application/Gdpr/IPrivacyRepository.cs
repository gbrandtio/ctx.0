using CtxApp.Domain.Gdpr;

namespace CtxApp.Application.Gdpr;

public interface IPrivacyRepository
{
    Task<ConsentRecord?> GetLatestConsentAsync(Guid userId, CancellationToken ct = default);
    void AddConsent(ConsentRecord record);

    Task<DataExportJob?> GetExportJobAsync(Guid id, CancellationToken ct = default);
    void AddExportJob(DataExportJob job);
    Task<List<DataExportJob>> GetStaleExportJobsAsync(Guid userId, DateTimeOffset now, CancellationToken ct = default);

    Task<CtxApp.Domain.Auth.UserCredential?> GetUserCredentialAsync(Guid userId, CancellationToken ct = default);
}
