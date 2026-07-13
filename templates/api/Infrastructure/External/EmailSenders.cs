using Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Infrastructure.External;

/// <summary>
/// Development email sender: logs the code instead of sending. Swap for a
/// real provider (SES, SendGrid, SMTP) in production — the port is
/// IEmailSender; nothing else changes.
/// </summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendVerificationCodeAsync(string email, string code, CancellationToken ct)
    {
        // The address is PII: mask everything before the @.
        var at = email.IndexOf('@');
        var masked = at > 1 ? $"{email[0]}***{email[at..]}" : "***";
        logger.LogInformation(
            "Signup verification code for {Email}: {Code}", masked, code);
        return Task.CompletedTask;
    }
}
