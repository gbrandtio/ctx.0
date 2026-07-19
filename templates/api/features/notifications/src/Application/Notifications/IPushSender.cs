namespace CtxApp.Application.Notifications;

/// <summary>
/// Delivers a push notification to a set of device tokens. Infrastructure supplies
/// the implementation: a real FCM sender when credentials are configured, or a
/// logging no-op otherwise, so the API runs end to end without external setup.
/// </summary>
public interface IPushSender
{
    Task SendAsync(IReadOnlyList<string> tokens, string title, string body, CancellationToken cancellationToken = default);
}
