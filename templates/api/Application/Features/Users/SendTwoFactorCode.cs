// ctx:auth_2fa_email:begin
using System.Security.Cryptography;
using Application.Abstractions;
using Domain.Entities;
using MediatR;

namespace Application.Features.Users;

public sealed record SendTwoFactorCodeCommand(long UserId, string Email) : IRequest;

public sealed class SendTwoFactorCodeHandler(
    ISignupVerificationRepository verifications,
    IBlindIndexProvider blindIndex,
    IEmailSender emails,
    IIdGenerator ids,
    IClock clock) : IRequestHandler<SendTwoFactorCodeCommand>
{
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);

    public async Task Handle(SendTwoFactorCodeCommand request, CancellationToken ct)
    {
        var emailHash = blindIndex.ComputeHash(request.Email);

        var existing = await verifications.FindActiveByEmailHashAsync(emailHash, ct);
        if (existing is not null)
        {
            verifications.Remove(existing);
        }

        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        verifications.Add(new SignupVerification
        {
            Id = ids.NextId(),
            EmailHash = emailHash,
            CodeHash = blindIndex.ComputeHash(code),
            ExpiresAt = clock.UtcNow.Add(CodeLifetime),
            CreatedAt = clock.UtcNow,
        });
        await verifications.SaveChangesAsync(ct);

        await emails.SendVerificationCodeAsync(request.Email, code, ct);
    }
}
// ctx:auth_2fa_email:end
