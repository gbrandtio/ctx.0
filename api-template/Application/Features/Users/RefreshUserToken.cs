using Application.Abstractions;
using Application.Common;
using Contracts.Auth;
using Domain.Constants;
using Domain.Exceptions;
using MediatR;
using SharedKernel.Clock;

namespace Application.Features.Users;

public sealed record RefreshUserTokenCommand(string RefreshToken) : IRequest<AuthResponse>;

/// <summary>
/// Single-use rotation with reuse detection (AUTHENTICATION.md — Refresh
/// Token Security): a replayed (already-revoked) token revokes the whole
/// family.
/// </summary>
public sealed class RefreshUserTokenHandler(
    IRefreshTokenRepository refreshTokens,
    IUserRepository users,
    IJwtTokenService jwt,
    TokenIssuer tokenIssuer,
    IClock clock) : IRequestHandler<RefreshUserTokenCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(RefreshUserTokenCommand command, CancellationToken ct)
    {
        var hash = jwt.HashRefreshToken(command.RefreshToken);
        var token = await refreshTokens.FindByHashAsync(hash, ct)
            ?? throw DomainException.Unauthorized("Invalid refresh token.");

        if (token.IsRevoked)
        {
            await refreshTokens.RevokeFamilyAsync(token.FamilyId, ct);
            throw DomainException.Unauthorized(
                "Refresh token reuse detected. All sessions revoked.");
        }

        if (token.ExpiresAt < clock.UtcNow)
        {
            throw DomainException.Unauthorized("Refresh token has expired.");
        }

        if (token.UserType != SecurityConstants.Roles.User)
        {
            throw DomainException.Unauthorized("Invalid refresh token.");
        }

        var user = await users.GetByIdAsync(token.UserId, ct);
        if (user is null || user.IsAnonymized)
        {
            throw DomainException.Unauthorized("Invalid refresh token.");
        }

        await refreshTokens.RevokeAsync(token.Id, ct);

        return await tokenIssuer.IssueAsync(
            new AccessTokenSubject(user.Id, user.Username, SecurityConstants.Roles.User),
            user.Email, token.FamilyId, ct);
    }
}
