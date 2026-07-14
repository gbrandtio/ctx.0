using Application.Abstractions;
using Contracts.Users;
using Domain.Exceptions;
using MediatR;

namespace Application.Features.Users;

public sealed record GetUserQuery(long UserId) : IRequest<UserResponse>;

public sealed class GetUserHandler(IUserRepository users)
    : IRequestHandler<GetUserQuery, UserResponse>
{
    public async Task<UserResponse> Handle(GetUserQuery query, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(query.UserId, ct);
        if (user is null || user.IsAnonymized)
        {
            throw DomainException.NotFound("User not found.");
        }
        return new UserResponse(user.Id, user.Username, user.Email, user.Name,
            user.HasTrackingConsent, user.CreatedAt);
    }
}
