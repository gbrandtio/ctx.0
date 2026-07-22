namespace CtxApp.Application.Notes;

public sealed record NoteDto(Guid Id, string Title, string Body, DateTimeOffset CreatedAt);

public interface INotesService
{
    Task<Guid> CreateNoteAsync(Guid userId, string title, string body, CancellationToken cancellationToken = default);
    Task<List<NoteDto>> GetNotesAsync(CancellationToken cancellationToken = default);
    Task<List<NoteDto>> SearchNotesAsync(string title, CancellationToken cancellationToken = default);
}
