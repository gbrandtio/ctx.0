using System.Security.Cryptography;
using System.Text;
using CtxApp.Api.Gdpr;
using CtxApp.Api.Localization;
using CtxApp.Application.Abstractions;
using CtxApp.Domain.Auth;
using CtxApp.Domain.Gdpr;
using CtxApp.Infrastructure.Gdpr;
using CtxApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace CtxApp.Api.Endpoints;

public sealed record ConsentDecisionRequest(string PolicyVersion, string[]? Purposes, string? Source);
public sealed record DeleteAccountRequest(string Password, string Confirm);

/// <summary>
/// The data-subject rights the app exposes to a signed-in user: recording and
/// withdrawing consent (Art. 7), taking a copy of their data (Art. 15/20), and
/// deleting the account (Art. 17). Everything here is RLS-scoped to the caller, so
/// one user can never reach another's consent trail or export. Requires authentication.
/// </summary>
public static class PrivacyEndpoints
{
    public static IEndpointRouteBuilder MapPrivacyEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/privacy").RequireAuthorization();

        // --- Consent -------------------------------------------------------

        group.MapGet("/consent", async (CtxAppDbContext db, ICurrentUser user, GdprOptions options, CancellationToken ct) =>
        {
            var latest = await LatestConsentAsync(db, user.UserId!.Value, ct);
            return Results.Ok(new { policyVersion = options.PolicyVersion, consent = Present(latest) });
        });

        group.MapPut("/consent", async (
            ConsentDecisionRequest body,
            CtxAppDbContext db,
            ICurrentUser user,
            GdprOptions options,
            IStringLocalizer<Messages> loc,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.PolicyVersion))
            {
                return Results.BadRequest(new { error = loc["gdpr.policyVersionRequired"].Value });
            }

            // Append rather than update: the history of decisions is the evidence
            // that consent was given, and withdrawing is just a narrower decision.
            var record = new ConsentRecord
            {
                UserId = user.UserId!.Value,
                PolicyVersion = body.PolicyVersion,
                Purposes = string.Join(',', (body.Purposes ?? []).Where(p => !string.IsNullOrWhiteSpace(p)).Distinct()),
                Source = string.IsNullOrWhiteSpace(body.Source) ? "app" : body.Source,
            };
            db.Set<ConsentRecord>().Add(record);
            await db.SaveChangesAsync(ct);

            return Results.Ok(new { policyVersion = options.PolicyVersion, consent = Present(record) });
        });

        // --- Export --------------------------------------------------------

        group.MapPost("/export", async (
            CtxAppDbContext db,
            ICurrentUser user,
            ExportJobQueue queue,
            ExportArchiveStore archives,
            ITokenGenerator tokens,
            ITokenHasher hasher,
            IClock clock,
            CancellationToken ct) =>
        {
            var userId = user.UserId!.Value;
            await PurgeExpiredAsync(db, archives, userId, clock, ct);

            var token = tokens.NewToken();
            var job = new DataExportJob
            {
                UserId = userId,
                StorageKey = Guid.NewGuid().ToString("n"),
                DownloadTokenHash = hasher.Hash(token),
            };
            db.Set<DataExportJob>().Add(job);
            await db.SaveChangesAsync(ct);

            await queue.EnqueueAsync(new ExportTicket(job.Id, userId), ct);

            // The token is shown exactly once; only its hash is stored.
            return Results.Accepted($"/v1/privacy/export/{job.Id}", new
            {
                jobId = job.Id,
                status = job.Status.ToString(),
                downloadToken = token,
            });
        });

        group.MapGet("/export/{id:guid}", async (Guid id, CtxAppDbContext db, CancellationToken ct) =>
        {
            var job = await db.Set<DataExportJob>().AsNoTracking().FirstOrDefaultAsync(j => j.Id == id, ct);
            return job is null
                ? Results.NotFound()
                : Results.Ok(new
                {
                    jobId = job.Id,
                    status = job.Status.ToString(),
                    job.CreatedAt,
                    job.CompletedAt,
                    job.ExpiresAt,
                    job.DownloadedAt,
                    job.SizeBytes,
                    job.Error,
                });
        });

        group.MapGet("/export/{id:guid}/download", async (
            Guid id,
            string? token,
            CtxAppDbContext db,
            ExportArchiveStore archives,
            ITokenHasher hasher,
            IClock clock,
            IStringLocalizer<Messages> loc,
            CancellationToken ct) =>
        {
            var job = await db.Set<DataExportJob>().FirstOrDefaultAsync(j => j.Id == id, ct);
            if (job is null)
            {
                return Results.NotFound();
            }
            if (string.IsNullOrEmpty(token) || !FixedTimeEquals(hasher.Hash(token), job.DownloadTokenHash))
            {
                return Results.Json(new { error = loc["gdpr.invalidDownloadToken"].Value }, statusCode: StatusCodes.Status401Unauthorized);
            }
            if (job.Status != DataExportStatus.Ready)
            {
                return Results.Json(
                    new { error = loc["gdpr.exportNotReady", job.Status.ToString().ToLowerInvariant()].Value, job.Error },
                    statusCode: StatusCodes.Status409Conflict);
            }
            if (job.DownloadedAt is not null || job.ExpiresAt <= clock.UtcNow)
            {
                if (job.DownloadedAt is null)
                {
                    await ExpireAsync(db, archives, job, ct);
                }
                return Results.Json(
                    new { error = loc["gdpr.exportConsumed"].Value },
                    statusCode: StatusCodes.Status410Gone);
            }

            var bundle = await archives.ReadAsync(job.StorageKey, ct);

            // A bundle is handed over once: the archive goes as it is served, so a
            // leaked link cannot be replayed and the plaintext-adjacent copy on
            // disk lives no longer than it must.
            job.DownloadedAt = clock.UtcNow;
            archives.Delete(job.StorageKey);
            await db.SaveChangesAsync(ct);

            return Results.File(bundle, "application/zip", $"ctx-export-{job.Id:n}.zip");
        });

        // --- Erasure -------------------------------------------------------

        group.MapPost("/account/delete", async (
            DeleteAccountRequest body,
            CtxAppDbContext db,
            ICurrentUser user,
            IPasswordHasher passwords,
            AccountEraser eraser,
            IStringLocalizer<Messages> loc,
            CancellationToken ct) =>
        {
            if (!string.Equals(body.Confirm, "DELETE", StringComparison.Ordinal))
            {
                return Results.BadRequest(new { error = loc["gdpr.confirmDelete"].Value });
            }

            var userId = user.UserId!.Value;
            var credential = await db.Set<UserCredential>().AsNoTracking().FirstOrDefaultAsync(c => c.UserId == userId, ct);
            if (credential is null || !passwords.Verify(body.Password, credential.PasswordHash))
            {
                return Results.Json(new { error = loc["gdpr.passwordMismatch"].Value }, statusCode: StatusCodes.Status401Unauthorized);
            }

            await eraser.EraseAsync(userId, ct);
            return Results.NoContent();
        });

        return app;
    }

    private static Task<ConsentRecord?> LatestConsentAsync(CtxAppDbContext db, Guid userId, CancellationToken ct) =>
        db.Set<ConsentRecord>()
            .AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.DecidedAt)
            .FirstOrDefaultAsync(ct);

    private static object? Present(ConsentRecord? record) =>
        record is null
            ? null
            : new
            {
                record.PolicyVersion,
                Purposes = record.Purposes.Length == 0 ? Array.Empty<string>() : record.Purposes.Split(','),
                record.Source,
                record.DecidedAt,
            };

    /// <summary>
    /// Drop the caller's aged-out exports and their archives. Run when a new export
    /// is requested: RLS scopes every query to one user, so retention is enforced
    /// on the user's own rows instead of from a privileged sweep. A bundle that was
    /// downloaded keeps its <c>Ready</c> status and its <c>DownloadedAt</c> stamp —
    /// the row is the record of what happened — and its archive is already gone.
    /// </summary>
    private static async Task PurgeExpiredAsync(
        CtxAppDbContext db, ExportArchiveStore archives, Guid userId, IClock clock, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var stale = await db.Set<DataExportJob>()
            .Where(j => j.UserId == userId
                && j.DownloadedAt == null
                && j.ExpiresAt != null
                && j.ExpiresAt <= now)
            .ToListAsync(ct);

        foreach (var job in stale)
        {
            archives.Delete(job.StorageKey);
            job.Status = DataExportStatus.Expired;
        }
        if (stale.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }
    }

    private static async Task ExpireAsync(
        CtxAppDbContext db, ExportArchiveStore archives, DataExportJob job, CancellationToken ct)
    {
        archives.Delete(job.StorageKey);
        job.Status = DataExportStatus.Expired;
        await db.SaveChangesAsync(ct);
    }

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
