using CtxApp.Application.Abstractions;
using CtxApp.Domain.Notes;
using CtxApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CtxApp.Infrastructure.Gdpr;

/// <summary>
/// The notes feature's personal data: every note the user wrote, decrypted. The
/// title's blind index is a derived search value and is left out of the export.
/// </summary>
public sealed class NotesPersonalData(CtxAppDbContext dbContext) : IPersonalDataContributor
{
    public string Section => "notes";

    public async Task<object?> ExportAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var notes = await dbContext.Set<Note>()
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderBy(n => n.CreatedAt)
            .Select(n => new { n.Id, n.Title, n.Body, n.CreatedAt })
            .ToListAsync(cancellationToken);

        return notes.Count == 0 ? null : notes;
    }

    public async Task EraseAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var notes = await dbContext.Set<Note>().Where(n => n.UserId == userId).ToListAsync(cancellationToken);
        dbContext.Set<Note>().RemoveRange(notes);
    }
}
