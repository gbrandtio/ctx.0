using Application.Abstractions;
using Application.Common;
using Contracts.Auth;
using Domain.Constants;
using Domain.Entities;
using Domain.Exceptions;
using MediatR;

namespace Application.Features.Users.Google;

public sealed record AuthenticateGoogleUserCommand(string IdToken) : IRequest<AuthResponse>;

/// <summary>
/// Validates the Google ID token, then links to an existing user (by
/// hashed subject, else by email) or creates a new one
/// (AUTHENTICATION.md — Google OAuth).
/// </summary>
public sealed class AuthenticateGoogleUserHandler(
    IGoogleTokenValidator google,
    IGoogleIdentityRepository googleIdentities,
    IUserRepository users,
    IBlindIndexProvider blindIndex,
    TokenIssuer tokenIssuer,
    IIdGenerator ids,
    IClock clock) : IRequestHandler<AuthenticateGoogleUserCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(
        AuthenticateGoogleUserCommand command, CancellationToken ct)
    {
        var info = await google.ValidateAsync(command.IdToken, ct);
        var subjectHash = blindIndex.ComputeHash(info.Subject);

        var identity = await googleIdentities.FindBySubjectHashAsync(subjectHash, ct);
        User user;
        if (identity is not null)
        {
            user = (await users.GetByIdAsync(identity.UserId, ct))!;

            // Defence in depth: a delete severs the Google link, but never
            // re-enter an anonymized account even if a stale link survives (H4).
            if (user.IsAnonymized)
            {
                throw DomainException.Unauthorized("Invalid credentials provided.");
            }
        }
        else
        {
            var email = info.Email.Trim().ToLowerInvariant();
            var emailHash = blindIndex.ComputeHash(email);
            var existing = await users.FindByEmailHashAsync(emailHash, ct);
            if (existing is not null)
            {
                user = existing;
            }
            else
            {
                var username = $"user{ids.NextId() % 1_000_000_000}";
                user = new User
                {
                    Id = ids.NextId(),
                    Username = username,
                    Email = email,
                    Name = info.Name,
                    UsernameHash = blindIndex.ComputeHash(username),
                    EmailHash = emailHash,
                    PasswordHash = null, // Google-only account
                    CreatedAt = clock.UtcNow,
                    UpdatedAt = clock.UtcNow,
                };
                users.Add(user);
                await users.SaveChangesAsync(ct);
            }

            googleIdentities.Add(new UserGoogleIdentity
            {
                Id = ids.NextId(),
                UserId = user.Id,
                GoogleSubjectHash = subjectHash,
                CreatedAt = clock.UtcNow,
            });
            await googleIdentities.SaveChangesAsync(ct);
        }

        return await tokenIssuer.IssueAsync(
            new AccessTokenSubject(user.Id, user.Username, SecurityConstants.Roles.User),
            user.Email, familyId: null, ct);
    }
}
