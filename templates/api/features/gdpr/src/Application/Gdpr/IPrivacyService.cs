using CtxApp.Domain.Auth;
using CtxApp.Domain.Gdpr;

namespace CtxApp.Application.Gdpr;

public sealed record ConsentDto(string PolicyVersion, string[] Purposes, string Source, DateTimeOffset DecidedAt);
public sealed record ExportJobDto(Guid JobId, string Status, DateTimeOffset CreatedAt, DateTimeOffset? CompletedAt, DateTimeOffset? ExpiresAt, DateTimeOffset? DownloadedAt, long SizeBytes, string? Error);
public sealed record ExportJobCreatedDto(Guid JobId, string Status, string DownloadToken);

public interface IPrivacyService
{
    Task<ConsentDto?> GetLatestConsentAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ConsentDto> RecordConsentAsync(Guid userId, string policyVersion, string[] purposes, string? source, CancellationToken cancellationToken = default);
    
    Task<ExportJobCreatedDto> RequestExportAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ExportJobDto?> GetExportJobAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(byte[] Bundle, string ContentType, string FileName)?> DownloadExportAsync(Guid id, string token, CancellationToken cancellationToken = default);

    Task<bool> VerifyPasswordAsync(Guid userId, string password, CancellationToken cancellationToken = default);
}
