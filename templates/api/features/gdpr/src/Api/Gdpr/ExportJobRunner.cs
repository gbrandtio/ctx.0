using CtxApp.Application.Abstractions;
using CtxApp.Application.Gdpr;
using CtxApp.Domain.Gdpr;
using CtxApp.Infrastructure.Gdpr;
using CtxApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CtxApp.Api.Gdpr;

/// <summary>
/// Builds queued export bundles off the request thread. Each job runs in its own
/// scope with the requesting user declared as the <see cref="PersonalDataSubject"/>,
/// so Row-Level Security scopes the export to exactly that user's rows even though
/// there is no HTTP principal here. A failure is recorded on the job rather than
/// thrown, so one bad export never stops the queue.
/// </summary>
public sealed class ExportJobRunner(
    ExportJobQueue queue,
    IServiceProvider services,
    ILogger<ExportJobRunner> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var ticket in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await RunAsync(ticket, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task RunAsync(ExportTicket ticket, CancellationToken ct)
    {
        using var scope = services.CreateScope();
        using var subject = PersonalDataSubject.Enter(ticket.UserId);

        var db = scope.ServiceProvider.GetRequiredService<CtxAppDbContext>();
        var job = await db.Set<DataExportJob>().FirstOrDefaultAsync(j => j.Id == ticket.JobId, ct);
        if (job is null)
        {
            return;
        }

        try
        {
            var exporter = scope.ServiceProvider.GetRequiredService<PersonalDataExporter>();
            var archives = scope.ServiceProvider.GetRequiredService<ExportArchiveStore>();
            var options = scope.ServiceProvider.GetRequiredService<GdprOptions>();
            var clock = scope.ServiceProvider.GetRequiredService<IClock>();

            var bundle = await exporter.BuildArchiveAsync(ticket.UserId, ct);
            await archives.WriteAsync(job.StorageKey, bundle, ct);

            job.SizeBytes = bundle.LongLength;
            job.CompletedAt = clock.UtcNow;
            job.ExpiresAt = clock.UtcNow + options.ExportTtl;
            job.Status = DataExportStatus.Ready;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Data export {JobId} failed.", job.Id);
            job.Status = DataExportStatus.Failed;
            job.Error = ex.Message;
        }

        await db.SaveChangesAsync(ct);
    }
}
