using Acme.Application.Abstractions;
using Acme.Domain.Notes;
using Acme.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Acme.Api.Endpoints;

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

        group.MapPost("/", async (CreateNoteRequest body, AcmeDbContext db, ICurrentUser user, IBlindIndex blindIndex) =>
        {
            var note = new Note
            {
                UserId = user.UserId!.Value,
                Title = body.Title,
                TitleBlindIndex = blindIndex.Compute(body.Title),
                Body = body.Body,
            };
            db.Set<Note>().Add(note);
            await db.SaveChangesAsync();
            return Results.Ok(new { note.Id });
        });

        group.MapGet("/", async (AcmeDbContext db) =>
        {
            var notes = await db.Set<Note>().OrderByDescending(n => n.CreatedAt).ToListAsync();
            return Results.Ok(notes.Select(n => new { n.Id, n.Title, n.Body, n.CreatedAt }));
        });

        group.MapGet("/search", async (string title, AcmeDbContext db, IBlindIndex blindIndex) =>
        {
            var index = blindIndex.Compute(title);
            var notes = await db.Set<Note>().Where(n => n.TitleBlindIndex == index).ToListAsync();
            return Results.Ok(notes.Select(n => new { n.Id, n.Title, n.Body }));
        });

        return app;
    }
}
