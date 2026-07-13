using Ctx0.Security.Abstractions;

namespace Ctx0.Security;

/// <summary>
/// AsyncLocal ambient identity consumed by the RlsInterceptor. The API
/// sets the user id from JWT claims per request; background workers open
/// a system-bypass scope to run as app_internal_worker
/// (DATABASE_RLS_POLICIES.md §3). Registered as a singleton — the state
/// is per-async-flow, not per-instance.
/// </summary>
public sealed class CurrentUserContext : ICurrentUserProvider
{
    private static readonly AsyncLocal<long?> CurrentUserId = new();
    private static readonly AsyncLocal<bool> SystemBypass = new();

    public long? UserId => CurrentUserId.Value;
    public bool IsSystemBypassActive => SystemBypass.Value;

    public void SetUser(long? userId) => CurrentUserId.Value = userId;

    public IDisposable BeginSystemBypassScope()
    {
        SystemBypass.Value = true;
        return new BypassScope();
    }

    private sealed class BypassScope : IDisposable
    {
        public void Dispose() => SystemBypass.Value = false;
    }
}
