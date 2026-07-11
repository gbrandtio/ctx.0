using Application.Abstractions;
using Contracts.Users;
using Domain.Exceptions;
using MediatR;
using SharedKernel.Clock;

namespace Application.Features.Users;

public sealed record UpdateUserCommand(long UserId, UpdateUserRequest Request)
    : IRequest<UserResponse>;

public sealed class UpdateUserHandler(IUserRepository users, IClock clock)
    : IRequestHandler<UpdateUserCommand, UserResponse>
{
    public async Task<UserResponse> Handle(UpdateUserCommand command, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(command.UserId, ct);
        if (user is null || user.IsAnonymized)
        {
            throw DomainException.NotFound("User not found.");
        }

        user.Name = command.Request.Name?.Trim();
        user.UpdatedAt = clock.UtcNow;
        await users.SaveChangesAsync(ct);

        return new UserResponse(user.Id, user.Username, user.Email, user.Name, user.CreatedAt);
    }
}
