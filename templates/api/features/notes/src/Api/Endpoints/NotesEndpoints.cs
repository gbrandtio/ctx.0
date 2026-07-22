using CtxApp.Application.Abstractions;
using CtxApp.Domain.Notes;
using CtxApp.Application.Notes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CtxApp.Api.Endpoints;

public sealed record CreateNoteRequest(string Title, string Body);

/// <summary>
/// User notes with encrypted title/body and per-user isolation. Every query is
/// scoped by RLS to the authenticated user; titles are searchable via a blind
/// index. Requires authentication.
/// </summary>
public static class NotesEndpoints
{
    public static IEndpointRouteBuilder MapNotesEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/notes").RequireAuthorization();

        group.MapPost("/", async (CreateNoteRequest body, INotesService notesService, ICurrentUser user, CancellationToken ct) =>
        {
            var id = await notesService.CreateNoteAsync(user.UserId!.Value, body.Title, body.Body, ct);
            return Results.Ok(new { Id = id });
        });

        group.MapGet("/", async (INotesService notesService, CancellationToken ct) =>
        {
            var notes = await notesService.GetNotesAsync(ct);
            return Results.Ok(notes);
        });

        group.MapGet("/search", async (string title, INotesService notesService, CancellationToken ct) =>
        {
            var notes = await notesService.SearchNotesAsync(title, ct);
            return Results.Ok(notes);
        });

        return app;
    }
}
