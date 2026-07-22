using CtxApp.Domain.Notes;

namespace CtxApp.Application.Notes;

public interface INotesRepository
{
    void Add(Note note);
    Task<List<Note>> GetAllAsync(CancellationToken ct = default);
    Task<List<Note>> SearchByTitleIndexAsync(string titleBlindIndex, CancellationToken ct = default);
}
