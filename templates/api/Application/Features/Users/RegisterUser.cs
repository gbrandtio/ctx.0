using Application.Abstractions;
using Application.Common;
using Contracts.Auth;
using Domain.Constants;
using Domain.Entities;
using Domain.Exceptions;
using Domain.ValueObjects;
using MediatR;

namespace Application.Features.Users;

public sealed record RegisterUserCommand(RegisterUserRequest Request) : IRequest<AuthResponse>;

/// <summary>
/// Registration requires a valid signup verification code, so the email
/// is verified at creation (AUTHENTICATION.md — API reference).
/// </summary>
public sealed class RegisterUserHandler(
    IUserRepository users,
    ISignupVerificationRepository verifications,
    IBlindIndexProvider blindIndex,
    IPasswordHasher passwordHasher,
    TokenIssuer tokenIssuer,
    IIdGenerator ids,
    IClock clock) : IRequestHandler<RegisterUserCommand, AuthResponse>
{
    public async Task<AuthResponse> Handle(RegisterUserCommand command, CancellationToken ct)
    {
        var request = command.Request;
        var email = Email.Create(request.Email);
        var password = Password.Create(request.Password);
        var username = request.Username.Trim();
        if (username.Length is < 3 or > 40)
        {
            throw new DomainException("Username must be between 3 and 40 characters.");
        }

        var emailHash = blindIndex.ComputeHash(email.Value);
        await ConsumeVerificationCodeAsync(emailHash, request.VerificationCode, ct);

        if (await users.EmailHashExistsAsync(emailHash, ct))
        {
            throw DomainException.Conflict("Email already exists.");
        }

        var user = new User
        {
            Id = ids.NextId(),
            Username = username,
            Email = email.Value,
            Name = request.Name?.Trim(),
            UsernameHash = blindIndex.ComputeHash(username.ToLowerInvariant()),
            EmailHash = emailHash,
            PasswordHash = passwordHasher.Hash(password.Value),
            CreatedAt = clock.UtcNow,
            UpdatedAt = clock.UtcNow,
        };
        users.Add(user);
        await users.SaveChangesAsync(ct);

        return await tokenIssuer.IssueAsync(
            new AccessTokenSubject(user.Id, username, SecurityConstants.Roles.User),
            email.Value, familyId: null, ct);
    }

    private async Task ConsumeVerificationCodeAsync(
        string emailHash, string code, CancellationToken ct)
    {
        var verification = await verifications.FindActiveByEmailHashAsync(emailHash, ct)
            ?? throw new DomainException("Invalid or expired verification code.");

        if (verification.ExpiresAt < clock.UtcNow ||
            verification.Attempts >= SignupVerification.MaxAttempts)
        {
            throw new DomainException("Invalid or expired verification code.");
        }

        verification.Attempts++;
        if (verification.CodeHash != blindIndex.ComputeHash(code))
        {
            await verifications.SaveChangesAsync(ct); // persist the attempt
            throw new DomainException("Invalid or expired verification code.");
        }

        verification.ConsumedAt = clock.UtcNow;
        await verifications.SaveChangesAsync(ct);
    }
}
