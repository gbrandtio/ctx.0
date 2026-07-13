using Application.Abstractions;
using MediatR;

namespace Application.Features.Users;

public sealed record LogoutUserCommand(long UserId, string RefreshToken) : IRequest;

/// <summary>
/// Idempotent revocation (AUTHENTICATION.md — Logout): revoking an
/// unknown or foreign token silently succeeds.
/// </summary>
public sealed class LogoutUserHandler(
    IRefreshTokenRepository refreshTokens,
    IJwtTokenService jwt) : IRequestHandler<LogoutUserCommand>
{
    public async Task Handle(LogoutUserCommand command, CancellationToken ct)
    {
        var hash = jwt.HashRefreshToken(command.RefreshToken);
        var token = await refreshTokens.FindByHashAsync(hash, ct);
        if (token is not null && token.UserId == command.UserId && !token.IsRevoked)
        {
            await refreshTokens.RevokeAsync(token.Id, ct);
        }
    }
}
