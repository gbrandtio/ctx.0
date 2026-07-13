using Application.Abstractions;
using Application.Common;
using Contracts.Auth;
using Domain.Constants;
using Domain.Exceptions;
using MediatR;

namespace Application.Features.Users;

public sealed record AuthenticateUserCommand(AuthenticateRequest Request) : IRequest<AuthResponse>;

public sealed class AuthenticateUserHandler(
    IUserRepository users,
    IBlindIndexProvider blindIndex,
    IPasswordHasher passwordHasher,
    TokenIssuer tokenIssuer
// ctx:auth_2fa_email:begin
    , IMediator mediator
// ctx:auth_2fa_email:end
    ) : IRequestHandler<AuthenticateUserCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(AuthenticateUserCommand command, CancellationToken ct)
    {
        var input = command.Request.UsernameOrEmail.Trim().ToLowerInvariant();
        var hash = blindIndex.ComputeHash(input);
        var user = await users.FindByUsernameOrEmailHashAsync(hash, ct);

        if (user?.PasswordHash is null || user.IsAnonymized)
        {
            // Constant-time behaviour: burn a bcrypt verification so timing
            // does not leak account existence (AUTHENTICATION.md).
            passwordHasher.DummyVerify();
            throw DomainException.Unauthorized("Invalid credentials provided.");
        }

        if (!passwordHasher.Verify(command.Request.Password, user.PasswordHash))
        {
            throw DomainException.Unauthorized("Invalid credentials provided.");
        }

// ctx:auth_2fa_email:begin
        await mediator.Send(new SendTwoFactorCodeCommand(user.Id, user.Email), ct);
        
        return new AuthResponse(
            AccessToken: null,
            RefreshToken: null,
            ExpiresAtUtc: null,
            UserId: user.Id,
            Username: user.Username,
            Email: user.Email,
            RequiresTwoFactor: true
        );
// ctx:auth_2fa_email:end

        return await tokenIssuer.IssueAsync(
            new AccessTokenSubject(user.Id, user.Username, SecurityConstants.Roles.User),
            user.Email, familyId: null, ct);
    }
}
