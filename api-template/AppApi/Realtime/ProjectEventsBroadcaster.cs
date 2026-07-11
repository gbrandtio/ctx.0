using System.Collections.Concurrent;
using System.Threading.Channels;

namespace AppApi.Realtime;

/// <summary>One event on a project's SSE stream (ADR-0003).</summary>
public sealed record ProjectEvent(long ProjectId, string Type, long? OrderId, string PayloadJson);

/// <summary>
/// In-memory connection registry (ADR-0003 §6): SSE requests subscribe to
/// a channel keyed by projectId; the Postgres listener publishes events
/// received via LISTEN/NOTIFY so every instance fans out its own
/// connections. Singleton.
/// </summary>
public sealed class ProjectEventsBroadcaster
{
    private readonly ConcurrentDictionary<long, ConcurrentDictionary<Guid, Channel<ProjectEvent>>>
        _subscribers = new();

    public void Publish(ProjectEvent projectEvent)
    {
        if (!_subscribers.TryGetValue(projectEvent.ProjectId, out var channels))
        {
            return;
        }
        foreach (var channel in channels.Values)
        {
            channel.Writer.TryWrite(projectEvent);
        }
    }

    public async IAsyncEnumerable<ProjectEvent> SubscribeAsync(
        long projectId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var id = Guid.NewGuid();
        var channel = Channel.CreateUnbounded<ProjectEvent>();
        var channels = _subscribers.GetOrAdd(
            projectId, _ => new ConcurrentDictionary<Guid, Channel<ProjectEvent>>());
        channels[id] = channel;
        try
        {
            await foreach (var projectEvent in channel.Reader.ReadAllAsync(ct))
            {
                yield return projectEvent;
            }
        }
        finally
        {
            channels.TryRemove(id, out _);
            if (channels.IsEmpty)
            {
                _subscribers.TryRemove(projectId, out _);
            }
        }
    }
}
