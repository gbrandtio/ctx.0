using Infrastructure.Persistence;
using Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.BackgroundJobs;

/// <summary>
/// Zero-downtime KEK rotation (ENVELOPE_ENCRYPTION_ARCHITECTURE.md §4):
/// on startup, scans in small batches for DEKs not wrapped with the
/// current KEK version and forces a save — the encryption interceptor
/// rewraps them with the active key.
/// </summary>
public sealed class KekRotationWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<KekRotationWorker> logger) : BackgroundService
{
    private const int BatchSize = 100;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var crypto = scope.ServiceProvider.GetRequiredService<AesEncryptionProvider>();
            var userContext = scope.ServiceProvider.GetRequiredService<CurrentUserContext>();
            using var bypass = userContext.BeginSystemBypassScope();

            var upgraded = 0;
            while (!stoppingToken.IsCancellationRequested)
            {
                await using var tx = await db.Database.BeginTransactionAsync(stoppingToken);
                var batch = await db.Users
                    .Where(u => u.EncryptedDek != string.Empty)
                    .OrderBy(u => u.Id)
                    .Skip(upgraded)
                    .Take(BatchSize)
                    .ToListAsync(stoppingToken);
                if (batch.Count == 0)
                {
                    await tx.CommitAsync(stoppingToken);
                    break;
                }

                var stale = batch.Where(u => !crypto.IsCurrentVersion(u.EncryptedDek)).ToList();
                foreach (var user in stale)
                {
                    // Touch the entity: the interceptor rewraps the DEK on save.
                    db.Entry(user).State = EntityState.Modified;
                }
                if (stale.Count > 0)
                {
                    await db.SaveChangesAsync(stoppingToken);
                }
                await tx.CommitAsync(stoppingToken);
                upgraded += batch.Count;
            }
            logger.LogInformation("KEK rotation scan complete ({Count} rows checked).", upgraded);
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
        catch (Exception e)
        {
            logger.LogError(e, "KEK rotation scan failed; stale DEKs upgrade lazily on save.");
        }
    }
}
