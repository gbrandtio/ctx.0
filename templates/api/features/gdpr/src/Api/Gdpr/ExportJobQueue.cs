using System.Threading.Channels;
using CtxApp.Application.Gdpr;

namespace CtxApp.Api.Gdpr;

/// <summary>
/// In-process hand-off from the request that asks for an export to the background
/// service that builds it. Single-instance by design, like the media feature's
/// local blob store: point both at a shared queue and store when you scale out.
/// A restart leaves a job row Pending; the user can ask again.
/// </summary>
public sealed class ExportJobQueue : IExportJobQueue
{
    private readonly Channel<ExportTicket> _channel = Channel.CreateUnbounded<ExportTicket>();

    public ValueTask EnqueueAsync(ExportTicket ticket, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(ticket, ct);

    public IAsyncEnumerable<ExportTicket> ReadAllAsync(CancellationToken ct)
        => _channel.Reader.ReadAllAsync(ct);
}
