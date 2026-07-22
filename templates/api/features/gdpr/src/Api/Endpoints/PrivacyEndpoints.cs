using CtxApp.Api.Gdpr;
using CtxApp.Api.Localization;
using CtxApp.Application.Abstractions;
using CtxApp.Application.Gdpr;
using CtxApp.Infrastructure.Gdpr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
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

        group.MapGet("/consent", async (IPrivacyService privacyService, ICurrentUser user, GdprOptions options, CancellationToken ct) =>
        {
            var latest = await privacyService.GetLatestConsentAsync(user.UserId!.Value, ct);
            return Results.Ok(new { policyVersion = options.PolicyVersion, consent = latest });
        });

        group.MapPut("/consent", async (
            ConsentDecisionRequest body,
            IPrivacyService privacyService,
            ICurrentUser user,
            GdprOptions options,
            IStringLocalizer<Messages> loc,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.PolicyVersion))
            {
                return Results.BadRequest(new { error = loc["gdpr.policyVersionRequired"].Value });
            }

            var consent = await privacyService.RecordConsentAsync(user.UserId!.Value, body.PolicyVersion, body.Purposes ?? [], body.Source, ct);

            return Results.Ok(new { policyVersion = options.PolicyVersion, consent });
        });

        // --- Export --------------------------------------------------------

        group.MapPost("/export", async (
            IPrivacyService privacyService,
            ICurrentUser user,
            CancellationToken ct) =>
        {
            var result = await privacyService.RequestExportAsync(user.UserId!.Value, ct);

            return Results.Accepted($"/v1/privacy/export/{result.JobId}", new
            {
                jobId = result.JobId,
                status = result.Status,
                downloadToken = result.DownloadToken,
            });
        });

        group.MapGet("/export/{id:guid}", async (Guid id, IPrivacyService privacyService, CancellationToken ct) =>
        {
            var job = await privacyService.GetExportJobAsync(id, ct);
            return job is null
                ? Results.NotFound()
                : Results.Ok(new
                {
                    jobId = job.JobId,
                    status = job.Status,
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
            IPrivacyService privacyService,
            IStringLocalizer<Messages> loc,
            CancellationToken ct) =>
        {
            try
            {
                var download = await privacyService.DownloadExportAsync(id, token ?? string.Empty, ct);
                if (download is null)
                {
                    return Results.NotFound();
                }

                return Results.File(download.Value.Bundle, download.Value.ContentType, download.Value.FileName);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Json(new { error = loc["gdpr.invalidDownloadToken"].Value }, statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("ready"))
            {
                return Results.Json(
                    new { error = loc["gdpr.exportNotReady", "processing"].Value },
                    statusCode: StatusCodes.Status409Conflict);
            }
            catch (InvalidOperationException)
            {
                return Results.Json(
                    new { error = loc["gdpr.exportConsumed"].Value },
                    statusCode: StatusCodes.Status410Gone);
            }
        });

        // --- Erasure -------------------------------------------------------

        group.MapPost("/account/delete", async (
            DeleteAccountRequest body,
            IPrivacyService privacyService,
            ICurrentUser user,
            AccountEraser eraser,
            IStringLocalizer<Messages> loc,
            CancellationToken ct) =>
        {
            if (!string.Equals(body.Confirm, "DELETE", StringComparison.Ordinal))
            {
                return Results.BadRequest(new { error = loc["gdpr.confirmDelete"].Value });
            }

            var userId = user.UserId!.Value;
            var isPasswordValid = await privacyService.VerifyPasswordAsync(userId, body.Password, ct);
            if (!isPasswordValid)
            {
                return Results.Json(new { error = loc["gdpr.passwordMismatch"].Value }, statusCode: StatusCodes.Status401Unauthorized);
            }

            await eraser.EraseAsync(userId, ct);
            return Results.NoContent();
        });

        return app;
    }
}
