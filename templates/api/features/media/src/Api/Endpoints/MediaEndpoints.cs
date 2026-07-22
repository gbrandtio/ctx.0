using CtxApp.Api.Localization;
using CtxApp.Application.Media;
using CtxApp.Domain.Media;
using CtxApp.Infrastructure.Media;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using CtxApp.Application.Abstractions;
using Microsoft.Extensions.Localization;

namespace CtxApp.Api.Endpoints;

/// <summary>
/// User file storage: multipart upload, list, authenticated download, and delete.
/// Metadata rows are RLS-scoped to the authenticated user and file names are
/// envelope-encrypted; blob bytes live in the <see cref="IBlobStore"/> encrypted
/// at rest. Requires authentication.
/// </summary>
public static class MediaEndpoints
{
    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/media").RequireAuthorization();

        group.MapPost("/", async (
            HttpRequest request,
            IMediaService mediaService,
            ICurrentUser user,
            IBlobStore blobs,
            MediaOptions options,
            IStringLocalizer<Messages> loc,
            CancellationToken ct) =>
        {
            if (!request.HasFormContentType)
            {
                return Results.BadRequest(new { error = loc["media.expectedMultipart"].Value });
            }

            var form = await request.ReadFormAsync(ct);
            var file = form.Files.GetFile("file");
            if (file is null || file.Length == 0)
            {
                return Results.BadRequest(new { error = loc["media.filePartRequired"].Value });
            }
            if (file.Length > options.MaxBytes)
            {
                return Results.Json(new { error = loc["media.tooLarge", options.MaxBytes].Value }, statusCode: StatusCodes.Status413PayloadTooLarge);
            }

            var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
            if (!options.IsAllowed(contentType))
            {
                return Results.Json(new { error = loc["media.contentTypeNotAllowed", contentType].Value }, statusCode: StatusCodes.Status415UnsupportedMediaType);
            }

            var key = Guid.NewGuid().ToString("n");
            await using (var upload = file.OpenReadStream())
            {
                await blobs.WriteAsync(key, upload, ct);
            }

            var fileName = string.IsNullOrWhiteSpace(file.FileName) ? "file" : Path.GetFileName(file.FileName);
            var mediaDto = await mediaService.CreateMediaAsync(user.UserId!.Value, fileName, contentType, file.Length, key, ct);

            return Results.Ok(new { mediaDto.Id, mediaDto.FileName, mediaDto.ContentType, mediaDto.SizeBytes, mediaDto.CreatedAt });
        });

        group.MapGet("/", async (IMediaService mediaService, CancellationToken ct) =>
        {
            var items = await mediaService.GetAllAsync(ct);
            return Results.Ok(new
            {
                items = items.Select(m => new { m.Id, m.FileName, m.ContentType, m.SizeBytes, m.CreatedAt }),
            });
        });

        group.MapGet("/{id:guid}", async (Guid id, IMediaService mediaService, IBlobStore blobs, CancellationToken ct) =>
        {
            // RLS scopes the lookup to the caller; another user's id resolves to null.
            var media = await mediaService.GetMediaObjectAsync(id, ct);
            if (media is null)
            {
                return Results.NotFound();
            }
            var stream = await blobs.ReadAsync(media.StorageKey, ct);
            return Results.File(stream, media.ContentType, media.FileName);
        });

        group.MapDelete("/{id:guid}", async (Guid id, IMediaService mediaService, IBlobStore blobs, CancellationToken ct) =>
        {
            var media = await mediaService.GetMediaObjectAsync(id, ct);
            if (media is null)
            {
                return Results.NotFound();
            }
            await mediaService.DeleteMediaAsync(media, ct);
            await blobs.DeleteAsync(media.StorageKey, ct);
            return Results.NoContent();
        });

        return app;
    }
}
