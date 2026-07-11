namespace Application.Abstractions;

/// <summary>
/// Ambient identity for the RLS layer (AUTHORIZATION.md §10). The
/// RlsInterceptor reads UserId to set app.current_user_id
/// transaction-locally; background workers activate the system bypass to
/// run as app_internal_worker (DATABASE_RLS_POLICIES.md §3).
/// </summary>
public interface ICurrentUserProvider
{
    long? UserId { get; }
    bool IsSystemBypassActive { get; }
}
