namespace CtxApp.Application.Gdpr;

public sealed record ExportTicket(Guid JobId, Guid UserId);

public interface IExportJobQueue
{
    ValueTask EnqueueAsync(ExportTicket ticket, CancellationToken ct = default);
    IAsyncEnumerable<ExportTicket> ReadAllAsync(CancellationToken ct = default);
}
