using System.Threading.Channels;

namespace CtxApp.Api.Gdpr;

/// <summary>A queued export: which job to build, and for whom.</summary>
public sealed record ExportTicket(Guid JobId, Guid UserId);

/// <summary>
/// In-process hand-off from the request that asks for an export to the background
/// service that builds it. Single-instance by design, like the media feature's
/// local blob store: point both at a shared queue and store when you scale out.
/// A restart leaves a job row Pending; the user can ask again.
/// </summary>
public sealed class ExportJobQueue
{
    private readonly Channel<ExportTicket> _channel = Channel.CreateUnbounded<ExportTicket>();

    public ValueTask EnqueueAsync(ExportTicket ticket, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(ticket, ct);

    public IAsyncEnumerable<ExportTicket> ReadAllAsync(CancellationToken ct)
        => _channel.Reader.ReadAllAsync(ct);
}
