using CtxApp.Application.Notes;
using CtxApp.Domain.Notes;
using Microsoft.EntityFrameworkCore;

namespace CtxApp.Infrastructure.Persistence;

public sealed class NotesRepository(CtxAppDbContext db) : INotesRepository
{
    public void Add(Note note) => db.Set<Note>().Add(note);

    public Task<List<Note>> GetAllAsync(CancellationToken ct = default) =>
        db.Set<Note>().OrderByDescending(n => n.CreatedAt).ToListAsync(ct);

    public Task<List<Note>> SearchByTitleIndexAsync(string titleBlindIndex, CancellationToken ct = default) =>
        db.Set<Note>().Where(n => n.TitleBlindIndex == titleBlindIndex).ToListAsync(ct);
}
