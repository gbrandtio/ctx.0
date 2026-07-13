// ctx:email_brevo:begin
namespace Infrastructure.External;

public sealed class BrevoOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string SenderName { get; set; } = "App Support";
    public string SenderEmail { get; set; } = "noreply@example.com";
}
// ctx:email_brevo:end
