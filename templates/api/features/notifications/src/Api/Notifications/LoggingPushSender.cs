using CtxApp.Application.Notifications;
using Microsoft.Extensions.Logging;

namespace CtxApp.Api.Notifications;

/// <summary>
/// The default <see cref="IPushSender"/>, used when no FCM credentials are
/// configured. It logs the intended delivery instead of sending, so the API runs
/// and its tests pass end to end offline. Set <c>NOTIFICATIONS_FCM_*</c> to switch
/// to real delivery via <see cref="FcmPushSender"/>.
/// </summary>
public sealed class LoggingPushSender(ILogger<LoggingPushSender> logger) : IPushSender
{
    public Task SendAsync(IReadOnlyList<string> tokens, string title, string body, CancellationToken cancellationToken = default)
    {
        foreach (var token in tokens)
        {
            logger.LogInformation(
                "Push not delivered (FCM not configured) to {Token}: {Title}",
                Redact(token),
                title);
        }
        return Task.CompletedTask;
    }

    private static string Redact(string token) =>
        token.Length <= 8 ? "***" : $"{token[..4]}…{token[^4..]}";
}
