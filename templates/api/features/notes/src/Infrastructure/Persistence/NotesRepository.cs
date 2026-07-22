using CtxApp.Application.Notes;
using CtxApp.Domain.Notes;
using Microsoft.EntityFrameworkCore;

namespace CtxApp.Infrastructure.Persistence;

public sealed class NotesRepository(CtxAppDbContext dbContext) : INotesRepository
{
    public void Add(Note note) => dbContext.Set<Note>().Add(note);

    public Task<List<Note>> GetAllAsync(CancellationToken cancellationToken = default) =>
        dbContext.Set<Note>().OrderByDescending(n => n.CreatedAt).ToListAsync(cancellationToken);

    public Task<List<Note>> SearchByTitleIndexAsync(string titleBlindIndex, CancellationToken cancellationToken = default) =>
        dbContext.Set<Note>().Where(n => n.TitleBlindIndex == titleBlindIndex).ToListAsync(cancellationToken);
}
