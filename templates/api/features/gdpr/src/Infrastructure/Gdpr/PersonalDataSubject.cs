using CtxApp.Application.Abstractions;
using Microsoft.AspNetCore.Http;
using CtxApp.Infrastructure.Security;

namespace CtxApp.Infrastructure.Gdpr;

/// <summary>
/// The user an export is being built for, when the work is not running on their
/// request. Row-Level Security scopes every query to <c>ICurrentUser.UserId</c>,
/// which is read from the HTTP principal — so a background export would see no
/// rows at all. The job runner declares its subject here for the duration of the
/// job, and <see cref="SubjectScopedCurrentUser"/> hands that id to RLS.
/// </summary>
public static class PersonalDataSubject
{
    private static readonly AsyncLocal<Guid?> Ambient = new();

    /// <summary>The subject declared for the current asynchronous flow, if any.</summary>
    public static Guid? Current => Ambient.Value;

    /// <summary>Declare <paramref name="userId"/> as the subject until the returned scope is disposed.</summary>
    public static IDisposable Enter(Guid userId)
    {
        var previous = Ambient.Value;
        Ambient.Value = userId;
        return new Scope(previous);
    }

    private sealed class Scope(Guid? previous) : IDisposable
    {
        public void Dispose() => Ambient.Value = previous;
    }
}

/// <summary>
/// <see cref="ICurrentUser"/> that prefers an explicitly declared
/// <see cref="PersonalDataSubject"/> and otherwise falls back to the request's
/// authenticated principal. Registered by <c>AddCtxGdpr</c> after the security
/// plane's registration, so it wins; on a normal request the ambient subject is
/// unset and behaviour is identical to <see cref="CurrentUser"/>.
/// </summary>
public sealed class SubjectScopedCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private readonly CurrentUser _request = new(accessor);

    public Guid? UserId => PersonalDataSubject.Current ?? _request.UserId;

    public bool IsAuthenticated => UserId is not null;
}
