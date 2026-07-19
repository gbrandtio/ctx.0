namespace CtxApp.Application.Abstractions;

/// <summary>
/// The authenticated principal for the current request. Infrastructure supplies
/// the implementation (from the validated JWT); RLS uses the id to scope rows.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    bool IsAuthenticated { get; }
}
