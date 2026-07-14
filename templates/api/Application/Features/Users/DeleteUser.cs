using Application.Abstractions;
using Domain.Constants;
using Domain.Exceptions;
using MediatR;

namespace Application.Features.Users;

public sealed record DeleteUserCommand(long UserId) : IRequest;

/// <summary>
/// GDPR anonymizing delete: PII is blanked (and re-encrypted), blind
/// indexes cleared so the row is unreachable by lookup, and every session
/// revoked. The row itself survives for ledger integrity.
/// </summary>
public sealed class DeleteUserHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IFirebaseIdentityRepository firebaseIdentities,
    IGoogleIdentityRepository googleIdentities,
    IClock clock) : IRequestHandler<DeleteUserCommand>
{
    public async Task Handle(DeleteUserCommand command, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(command.UserId, ct)
            ?? throw DomainException.NotFound("User not found.");

        user.Username = $"deleted-{user.Id}";
        user.Email = $"deleted-{user.Id}@anonymized.invalid";
        user.Name = null;
        user.UsernameHash = string.Empty;
        user.EmailHash = string.Empty;
        user.PasswordHash = null;
        user.IsAnonymized = true;
        user.UpdatedAt = clock.UtcNow;
        await users.SaveChangesAsync(ct);

        var fcm = await firebaseIdentities.FindByUserIdAsync(user.Id, ct);
        if (fcm is not null)
        {
            firebaseIdentities.Remove(fcm);
            await firebaseIdentities.SaveChangesAsync(ct);
        }

        // Sever the Google link so a later Google sign-in creates a fresh
        // account instead of re-entering this anonymized one (H4).
        await googleIdentities.RemoveForUserAsync(user.Id, ct);

        await refreshTokens.RevokeAllForUserAsync(
            user.Id, SecurityConstants.Roles.User, ct);
    }
}
