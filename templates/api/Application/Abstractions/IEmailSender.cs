namespace Application.Abstractions;

public interface IEmailSender
{
    Task SendVerificationCodeAsync(string email, string code, CancellationToken ct);
}
