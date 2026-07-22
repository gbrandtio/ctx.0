using CtxApp.Domain.Gdpr;

namespace CtxApp.Application.Gdpr;

public interface IPrivacyRepository
{
    Task<ConsentRecord?> GetLatestConsentAsync(Guid userId, CancellationToken cancellationToken = default);
    void AddConsent(ConsentRecord record);

    Task<DataExportJob?> GetExportJobAsync(Guid id, CancellationToken cancellationToken = default);
    void AddExportJob(DataExportJob job);
    Task<List<DataExportJob>> GetStaleExportJobsAsync(Guid userId, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<CtxApp.Domain.Auth.UserCredential?> GetUserCredentialAsync(Guid userId, CancellationToken cancellationToken = default);
}
