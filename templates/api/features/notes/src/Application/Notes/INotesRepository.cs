using CtxApp.Domain.Notes;

namespace CtxApp.Application.Notes;

public interface INotesRepository
{
    void Add(Note note);
    Task<List<Note>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<List<Note>> SearchByTitleIndexAsync(string titleBlindIndex, CancellationToken cancellationToken = default);
}
