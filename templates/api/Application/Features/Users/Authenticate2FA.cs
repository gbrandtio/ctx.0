// ctx:auth_2fa_email:begin
using Application.Abstractions;
using Application.Common;
using Contracts.Auth;
using Domain.Constants;
using Domain.Exceptions;
using MediatR;

namespace Application.Features.Users;

public sealed record Authenticate2FACommand(Authenticate2FARequest Request) : IRequest<AuthResponse>;

public sealed class Authenticate2FAHandler(
    IUserRepository users,
    ISignupVerificationRepository verifications,
    IPasswordHasher passwordHasher,
    IBlindIndexProvider blindIndex,
    TokenIssuer tokenIssuer) : IRequestHandler<Authenticate2FACommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(Authenticate2FACommand command, CancellationToken ct)
    {
        var input = command.Request.UsernameOrEmail.Trim().ToLowerInvariant();
        var hash = blindIndex.ComputeHash(input);
        var user = await users.FindByUsernameOrEmailHashAsync(hash, ct);

        if (user?.PasswordHash is null || user.IsAnonymized)
        {
            passwordHasher.DummyVerify();
            throw DomainException.Unauthorized("Invalid credentials provided.");
        }

        if (!passwordHasher.Verify(command.Request.Password, user.PasswordHash))
        {
            throw DomainException.Unauthorized("Invalid credentials provided.");
        }

        var emailHash = blindIndex.ComputeHash(user.Email);
        var verification = await verifications.FindActiveByEmailHashAsync(emailHash, ct)
                           ?? throw DomainException.Unauthorized("Invalid or expired code.");

        var codeHash = blindIndex.ComputeHash(command.Request.Code);
        if (verification.CodeHash != codeHash)
        {
            verification.Attempts++;
            if (verification.Attempts >= Domain.Entities.SignupVerification.MaxAttempts)
            {
                verifications.Remove(verification);
            }
            await verifications.SaveChangesAsync(ct);
            throw DomainException.Unauthorized("Invalid code.");
        }

        verifications.Remove(verification);
        await verifications.SaveChangesAsync(ct);

        return await tokenIssuer.IssueAsync(
            new AccessTokenSubject(user.Id, user.Username, SecurityConstants.Roles.User),
            user.Email, familyId: null, ct);
    }
}
// ctx:auth_2fa_email:end
