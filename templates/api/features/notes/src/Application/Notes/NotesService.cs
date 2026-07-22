using CtxApp.Application.Abstractions;
using CtxApp.Domain.Notes;

namespace CtxApp.Application.Notes;

public sealed class NotesService(INotesRepository notes, IUnitOfWork unitOfWork, IBlindIndex blindIndex) : INotesService
{
    public async Task<Guid> CreateNoteAsync(Guid userId, string title, string body, CancellationToken ct = default)
    {
        var note = new Note
        {
            UserId = userId,
            Title = title,
            TitleBlindIndex = blindIndex.Compute(title),
            Body = body,
        };
        notes.Add(note);
        await unitOfWork.SaveChangesAsync(ct);
        return note.Id;
    }

    public async Task<List<NoteDto>> GetNotesAsync(CancellationToken ct = default)
    {
        var entities = await notes.GetAllAsync(ct);
        return entities.Select(n => new NoteDto(n.Id, n.Title, n.Body, n.CreatedAt)).ToList();
    }

    public async Task<List<NoteDto>> SearchNotesAsync(string title, CancellationToken ct = default)
    {
        var index = blindIndex.Compute(title);
        var entities = await notes.SearchByTitleIndexAsync(index, ct);
        return entities.Select(n => new NoteDto(n.Id, n.Title, n.Body, n.CreatedAt)).ToList();
    }
}
