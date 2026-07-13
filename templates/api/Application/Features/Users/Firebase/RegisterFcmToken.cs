using Application.Abstractions;
using Domain.Entities;
using MediatR;

namespace Application.Features.Users.Firebase;

public sealed record RegisterFcmTokenCommand(long UserId, string Token) : IRequest;

/// <summary>
/// Upserts the caller's FCM token (NOTIFICATIONS.md §2). The token is PII
/// — the envelope-encryption interceptor encrypts it at rest.
/// </summary>
public sealed class RegisterFcmTokenHandler(
    IFirebaseIdentityRepository identities,
    IIdGenerator ids,
    IClock clock) : IRequestHandler<RegisterFcmTokenCommand>
{
    public async Task Handle(RegisterFcmTokenCommand command, CancellationToken ct)
    {
        var existing = await identities.FindByUserIdAsync(command.UserId, ct);
        if (existing is not null)
        {
            existing.Token = command.Token;
            existing.UpdatedAt = clock.UtcNow;
        }
        else
        {
            identities.Add(new UserFirebaseIdentity
            {
                Id = ids.NextId(),
                UserId = command.UserId,
                Token = command.Token,
                CreatedAt = clock.UtcNow,
                UpdatedAt = clock.UtcNow,
            });
        }
        await identities.SaveChangesAsync(ct);
    }
}

public sealed record UnregisterFcmTokenCommand(long UserId) : IRequest;

public sealed class UnregisterFcmTokenHandler(IFirebaseIdentityRepository identities)
    : IRequestHandler<UnregisterFcmTokenCommand>
{
    public async Task Handle(UnregisterFcmTokenCommand command, CancellationToken ct)
    {
        var existing = await identities.FindByUserIdAsync(command.UserId, ct);
        if (existing is not null)
        {
            identities.Remove(existing);
            await identities.SaveChangesAsync(ct);
        }
    }
}
