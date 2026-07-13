using System.Text.Json;
using Npgsql;

namespace AppApi.Realtime;

/// <summary>
/// Multi-instance SSE signaling (ADR-0003 §5): each API instance holds a
/// dedicated, NON-POOLED connection listening on payment_completed;
/// received payloads are fanned out to local SSE subscribers via the
/// singleton broadcaster.
/// </summary>
public sealed class PostgresPaymentUpdateListener(
    ProjectEventsBroadcaster broadcaster,
    IConfiguration configuration,
    ILogger<PostgresPaymentUpdateListener> logger) : BackgroundService
{
    public const string Channel = "payment_completed";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = new NpgsqlConnectionStringBuilder(
            configuration.GetConnectionString("Default"))
        {
            Pooling = false, // LISTEN requires a dedicated persistent connection
        }.ConnectionString;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(stoppingToken);
                connection.Notification += (_, args) => Fanout(args.Payload);

                await using (var listen = new NpgsqlCommand($"LISTEN {Channel};", connection))
                {
                    await listen.ExecuteNonQueryAsync(stoppingToken);
                }

                while (!stoppingToken.IsCancellationRequested)
                {
                    await connection.WaitAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                logger.LogError(e, "SSE listener connection failed; retrying.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private void Fanout(string payload)
    {
        try
        {
            using var json = JsonDocument.Parse(payload);
            var root = json.RootElement;
            broadcaster.Publish(new ProjectEvent(
                root.GetProperty("projectId").GetInt64(),
                root.TryGetProperty("type", out var type)
                    ? type.GetString() ?? "payment_completed"
                    : "payment_completed",
                root.TryGetProperty("orderId", out var orderId)
                    ? orderId.GetInt64()
                    : null,
                payload));
        }
        catch (Exception e)
        {
            logger.LogError(e, "Malformed SSE payload dropped.");
        }
    }
}
