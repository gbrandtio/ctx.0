namespace CtxApp.Application.Gdpr;

public sealed record ExportTicket(Guid JobId, Guid UserId);

public interface IExportJobQueue
{
    ValueTask EnqueueAsync(ExportTicket ticket, CancellationToken cancellationToken = default);
    IAsyncEnumerable<ExportTicket> ReadAllAsync(CancellationToken cancellationToken = default);
}
