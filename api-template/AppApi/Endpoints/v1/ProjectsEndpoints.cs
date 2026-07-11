using AppApi.Endpoints;
using AppApi.Middleware;
using AppApi.Realtime;
using Domain.Constants;

namespace AppApi.Endpoints.v1;

/// <summary>
/// Project realtime stream (ADR-0003): SSE with ALE + signing bypassed
/// (TLS + JWT + the ProjectRead ownership policy remain), 15s keep-alives,
/// and optional ?orderId= auto-termination.
/// </summary>
public sealed class ProjectsEndpoints : IEndpointModule
{
    private static readonly TimeSpan KeepAliveInterval = TimeSpan.FromSeconds(15);

    public void Map(IEndpointRouteBuilder v1)
    {
        v1.MapGet("/projects/{projectId:long}/events", (
                long projectId,
                long? orderId,
                ProjectEventsBroadcaster broadcaster,
                HttpContext context) =>
            {
                context.Response.Headers.ContentType = "text/event-stream";
                context.Response.Headers.CacheControl = "no-cache";
                // Disable proxy buffering (ADR-0003 — consequences).
                context.Response.Headers["X-Accel-Buffering"] = "no";
                return StreamAsync(projectId, orderId, broadcaster, context);
            })
            .WithMetadata(new AllowPlaintextAttribute())
            .WithMetadata(new SkipRequestSigningAttribute())
            .RequireAuthorization(SecurityConstants.Policies.ProjectRead);
    }

    private static async Task StreamAsync(
        long projectId, long? orderId,
        ProjectEventsBroadcaster broadcaster, HttpContext context)
    {
        var ct = context.RequestAborted;
        var events = broadcaster.SubscribeAsync(projectId, ct)
            .GetAsyncEnumerator(ct);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var nextEvent = events.MoveNextAsync().AsTask();
                var keepAlive = Task.Delay(KeepAliveInterval, ct);
                var completed = await Task.WhenAny(nextEvent, keepAlive);

                if (completed == keepAlive)
                {
                    // Comment frame keeps proxies from closing the stream.
                    await context.Response.WriteAsync(": keep-alive\n\n", ct);
                    await context.Response.Body.FlushAsync(ct);
                    continue;
                }

                if (!await nextEvent)
                {
                    break;
                }

                var projectEvent = events.Current;
                await context.Response.WriteAsync(
                    $"event: {projectEvent.Type}\ndata: {projectEvent.PayloadJson}\n\n", ct);
                await context.Response.Body.FlushAsync(ct);

                // Correlation auto-termination (ADR-0003 §6).
                if (orderId is not null && projectEvent.OrderId == orderId)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // client disconnected
        }
        finally
        {
            await events.DisposeAsync();
        }
    }
}
