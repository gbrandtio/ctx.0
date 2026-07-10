using Application.Abstractions;
using Domain.Constants;
using Domain.Exceptions;
using Domain.ValueObjects;
using MediatR;
using SharedKernel.Clock;

namespace Application.Features.Users;

public sealed record ChangePasswordCommand(
    long UserId, string CurrentPassword, string NewPassword) : IRequest;

/// <summary>
/// Verifies the old password and revokes every active session
/// (AUTHENTICATION.md — API reference).
/// </summary>
public sealed class ChangePasswordHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    IClock clock) : IRequestHandler<ChangePasswordCommand>
{
    public async Task Handle(ChangePasswordCommand command, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(command.UserId, ct)
            ?? throw DomainException.NotFound("User not found.");

        if (user.PasswordHash is null ||
            !passwordHasher.Verify(command.CurrentPassword, user.PasswordHash))
        {
            throw DomainException.Unauthorized("Invalid credentials provided.");
        }

        var newPassword = Password.Create(command.NewPassword);
        user.PasswordHash = passwordHasher.Hash(newPassword.Value);
        user.UpdatedAt = clock.UtcNow;
        await users.SaveChangesAsync(ct);

        await refreshTokens.RevokeAllForUserAsync(
            user.Id, SecurityConstants.Roles.User, ct);
    }
}
