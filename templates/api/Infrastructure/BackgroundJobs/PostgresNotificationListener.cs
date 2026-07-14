using FirebaseAdmin.Messaging;
using Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Infrastructure.BackgroundJobs;

/// <summary>
/// Outbox dispatcher (NOTIFICATIONS.md): LISTEN new_notification on a
/// dedicated, NON-POOLED connection; a startup catch-up scan covers
/// downtime windows; SemaphoreSlim bounds parallel FCM sends; sent_at is
/// set atomically and is the only "pending" signal. FCM tokens are
/// decrypted only here, in worker memory.
/// </summary>
public sealed class PostgresNotificationListener(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<PostgresNotificationListener> logger) : BackgroundService
{
    private const int MaxParallelSends = 10;
    private static readonly TimeSpan CatchUpWindow = TimeSpan.FromDays(7);
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim _sendSlots = new(MaxParallelSends);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!FirebaseIsConfigured())
        {
            logger.LogWarning("Firebase not configured — push dispatch disabled.");
            return;
        }

        // A periodic sweep is the safety net: any notification whose LISTEN
        // event was missed (connection down) or whose send failed transiently
        // stays sent_at NULL and is retried here rather than waiting for a
        // process restart (M6).
        _ = RunPeriodicSweepAsync(stoppingToken);

        var connectionString = new NpgsqlConnectionStringBuilder(
            configuration.GetConnectionString("Default"))
        {
            Pooling = false, // the LISTEN connection must never come from the pool
        }.ConnectionString;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(stoppingToken);
                connection.Notification += (_, args) =>
                    _ = DispatchByIdAsync(long.Parse(args.Payload), stoppingToken);

                await using (var listen = new NpgsqlCommand("LISTEN new_notification;", connection))
                {
                    await listen.ExecuteNonQueryAsync(stoppingToken);
                }

                // Catch up on every (re)connect — this covers the window
                // between losing the previous connection and establishing
                // this one, not just process startup (M6).
                await CatchUpAsync(stoppingToken);

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
                logger.LogError(e, "Notification listener connection failed; retrying.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task RunPeriodicSweepAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await CatchUpAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }

    private async Task CatchUpAsync(CancellationToken ct)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<Persistence.AppDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var userContext = scope.ServiceProvider.GetRequiredService<CurrentUserContext>();
            using var bypass = userContext.BeginSystemBypassScope();

            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var pendingIds = await db.UserNotifications
                .Where(n => n.SentAt == null && n.CreatedAt > clock.UtcNow - CatchUpWindow)
                .Select(n => n.Id)
                .ToListAsync(ct);
            await tx.CommitAsync(ct);

            foreach (var id in pendingIds)
            {
                await DispatchByIdAsync(id, ct);
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Notification catch-up scan failed.");
        }
    }

    private async Task DispatchByIdAsync(long notificationId, CancellationToken ct)
    {
        await _sendSlots.WaitAsync(ct);
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<Persistence.AppDbContext>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();
            var userContext = scope.ServiceProvider.GetRequiredService<CurrentUserContext>();
            using var bypass = userContext.BeginSystemBypassScope();

            await using var tx = await db.Database.BeginTransactionAsync(ct);

            var notification = await db.UserNotifications
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.SentAt == null, ct);
            if (notification is null)
            {
                return; // already dispatched (at-least-once tolerated)
            }

            // Materialization decrypts the token in memory only.
            var identity = await db.UserFirebaseIdentities
                .FirstOrDefaultAsync(f => f.UserId == notification.UserId, ct);

            if (identity is not null)
            {
                try
                {
                    await FirebaseMessaging.DefaultInstance.SendAsync(
                        new Message
                        {
                            Token = identity.Token,
                            Notification = new Notification
                            {
                                Title = notification.Title,
                                Body = notification.Body,
                            },
                            Data = new Dictionary<string, string>
                            {
                                ["type"] = notification.Type,
                                ["notificationId"] = notification.Id.ToString(),
                            },
                        }, ct);
                }
                catch (FirebaseMessagingException e) when (
                    e.MessagingErrorCode == MessagingErrorCode.Unregistered)
                {
                    // Self-cleaning invalid tokens (NOTIFICATIONS.md §2).
                    db.UserFirebaseIdentities.Remove(identity);
                }
            }

            await db.UserNotifications
                .Where(n => n.Id == notificationId)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(n => n.SentAt, clock.UtcNow), ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
        catch (Exception e)
        {
            logger.LogError(e, "Dispatch failed for notification {Id}.", notificationId);
        }
        finally
        {
            _sendSlots.Release();
        }
    }

    private static bool FirebaseIsConfigured() => FirebaseAdmin.FirebaseApp.DefaultInstance != null;
}
