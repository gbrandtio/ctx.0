// ctx:email_brevo:begin
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.External;

public sealed class BrevoEmailSender(
    HttpClient http,
    IOptions<BrevoOptions> options,
    ILogger<BrevoEmailSender> logger) : IEmailSender
{
    private readonly BrevoOptions _options = options.Value;

    public async Task SendVerificationCodeAsync(string email, string code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            logger.LogWarning("Brevo API key is not configured. Skipping email send for {Email}.", email);
            return;
        }

        var payload = new SendSmtpEmail
        {
            Sender = new Sender { Name = _options.SenderName, Email = _options.SenderEmail },
            To = [new Recipient { Email = email }],
            Subject = "Your verification code",
            HtmlContent = $"<html><body><p>Your verification code is: <strong>{code}</strong></p></body></html>"
        };

        var response = await http.PostAsJsonAsync("smtp/email", payload, ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            logger.LogError("Failed to send email via Brevo. Status: {StatusCode}, Error: {Error}", response.StatusCode, error);
        }
    }

    private class SendSmtpEmail
    {
        [JsonPropertyName("sender")]
        public Sender Sender { get; set; } = null!;

        [JsonPropertyName("to")]
        public List<Recipient> To { get; set; } = [];

        [JsonPropertyName("subject")]
        public string Subject { get; set; } = string.Empty;

        [JsonPropertyName("htmlContent")]
        public string HtmlContent { get; set; } = string.Empty;
    }

    private class Sender
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;
    }

    private class Recipient
    {
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;
    }
}
// ctx:email_brevo:end
