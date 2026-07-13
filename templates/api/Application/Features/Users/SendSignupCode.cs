using System.Security.Cryptography;
using Application.Abstractions;
using Domain.Entities;
using Domain.ValueObjects;
using MediatR;

namespace Application.Features.Users;

public sealed record SendSignupCodeCommand(string Email) : IRequest;

/// <summary>
/// Emails a 6-digit signup verification code (AUTHENTICATION.md — API
/// reference). The code is stored hashed; a fresh request replaces any
/// previous active code. The response is identical whether or not the
/// email is already registered (no account-existence oracle).
/// </summary>
public sealed class SendSignupCodeHandler(
    ISignupVerificationRepository verifications,
    IBlindIndexProvider blindIndex,
    IEmailSender emails,
    IIdGenerator ids,
    IClock clock) : IRequestHandler<SendSignupCodeCommand>
{
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(15);

    public async Task Handle(SendSignupCodeCommand request, CancellationToken ct)
    {
        var email = Email.Create(request.Email);
        var emailHash = blindIndex.ComputeHash(email.Value);

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

        await emails.SendVerificationCodeAsync(email.Value, code, ct);
    }
}
