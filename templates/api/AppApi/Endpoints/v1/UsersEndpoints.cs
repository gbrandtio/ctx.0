using System.Security.Claims;
using AppApi.Endpoints;
using Application.Features.Exports;
using Application.Features.Notifications;
using Application.Features.Users;
// ctx:push_firebase:begin
using Application.Features.Users.Firebase;
// ctx:push_firebase:end
// ctx:auth_google:begin
using Application.Features.Users.Google;
// ctx:auth_google:end
using Contracts.Auth;
using Contracts.Notifications;
using Contracts.Users;
using Domain.Constants;
using MediatR;

namespace AppApi.Endpoints.v1;

/// <summary>
/// User identity + self-service endpoints (AUTHENTICATION.md — API
/// reference). Identity always comes from JWT claims; {userId} routes are
/// gated by the UserSelf policy (automated IDOR check).
/// </summary>
public sealed class UsersEndpoints : IEndpointModule
{
    public void Map(IEndpointRouteBuilder v1)
    {
        var users = v1.MapGroup("/users");

        // ---- Anonymous auth flows ----
        users.MapPost("/register/send-code", async (
                SendSignupCodeRequest request, IMediator mediator, CancellationToken ct) =>
            {
                await mediator.Send(new SendSignupCodeCommand(request.Email), ct);
                return Results.Accepted();
            })
            .AllowAnonymous()
            .RequireRateLimiting("account_creation");

        users.MapPost("/", async (
                RegisterUserRequest request, IMediator mediator, CancellationToken ct) =>
                Results.Ok(await mediator.Send(new RegisterUserCommand(request), ct)))
            .AllowAnonymous()
            .RequireRateLimiting("account_creation");

        users.MapPost("/authenticate", async (
                AuthenticateRequest request, IMediator mediator, CancellationToken ct) =>
                Results.Ok(await mediator.Send(new AuthenticateUserCommand(request), ct)))
            .AllowAnonymous()
            .RequireRateLimiting("auth");

        // ctx:auth_google:begin
        users.MapPost("/google/authenticate", async (
                GoogleAuthenticateRequest request, IMediator mediator, CancellationToken ct) =>
                Results.Ok(await mediator.Send(
                    new AuthenticateGoogleUserCommand(request.IdToken), ct)))
            .AllowAnonymous()
            .RequireRateLimiting("auth");
        // ctx:auth_google:end

        users.MapPost("/refresh", async (
                RefreshRequest request, IMediator mediator, CancellationToken ct) =>
                Results.Ok(await mediator.Send(
                    new RefreshUserTokenCommand(request.RefreshToken), ct)))
            .AllowAnonymous()
            .RequireRateLimiting("auth");

        // ---- Authenticated session management ----
        users.MapPost("/logout", async (
                LogoutRequest request, ClaimsPrincipal user,
                IMediator mediator, CancellationToken ct) =>
            {
                await mediator.Send(
                    new LogoutUserCommand(UserId(user), request.RefreshToken), ct);
                return Results.NoContent();
            })
            .RequireAuthorization();

        users.MapPost("/change-password", async (
                ChangePasswordRequest request, ClaimsPrincipal user,
                IMediator mediator, CancellationToken ct) =>
            {
                await mediator.Send(new ChangePasswordCommand(
                    UserId(user), request.CurrentPassword, request.NewPassword), ct);
                return Results.NoContent();
            })
            .RequireAuthorization();

        // ---- Self-service profile ({userId} + UserSelf = automated IDOR) ----
        users.MapGet("/{userId:long}", async (
                long userId, IMediator mediator, CancellationToken ct) =>
                Results.Ok(await mediator.Send(new GetUserQuery(userId), ct)))
            .RequireAuthorization(SecurityConstants.Policies.UserSelf);

        users.MapPatch("/{userId:long}", async (
                long userId, UpdateUserRequest request,
                IMediator mediator, CancellationToken ct) =>
                Results.Ok(await mediator.Send(new UpdateUserCommand(userId, request), ct)))
            .RequireAuthorization(SecurityConstants.Policies.UserSelf);

        users.MapDelete("/{userId:long}", async (
                long userId, IMediator mediator, CancellationToken ct) =>
            {
                await mediator.Send(new DeleteUserCommand(userId), ct);
                return Results.NoContent();
            })
            .RequireAuthorization(SecurityConstants.Policies.UserSelf);

        users.MapPost("/{userId:long}/exports", async (
                long userId, IMediator mediator, CancellationToken ct) =>
            {
                await mediator.Send(new RequestUserExportCommand(userId), ct);
                return Results.Accepted();
            })
            .RequireAuthorization(SecurityConstants.Policies.UserSelf);

        // ---- Notifications feed (self-scoped via JWT, NOTIFICATIONS.md §4) ----
        users.MapGet("/notifications", async (
                ClaimsPrincipal user, IMediator mediator,
                int page, int pageSize, CancellationToken ct) =>
                Results.Ok(await mediator.Send(new GetNotificationsQuery(
                    UserId(user),
                    page == 0 ? 1 : page,
                    pageSize == 0 ? 20 : pageSize), ct)))
            .RequireAuthorization();

        // ---- FCM token registration (NOTIFICATIONS.md §2) ----
        // ctx:push_firebase:begin
        users.MapPost("/firebase/token", async (
                RegisterFcmTokenRequest request, ClaimsPrincipal user,
                IMediator mediator, CancellationToken ct) =>
            {
                await mediator.Send(
                    new RegisterFcmTokenCommand(UserId(user), request.Token), ct);
                return Results.NoContent();
            })
            .RequireAuthorization();

        users.MapDelete("/firebase/token", async (
                ClaimsPrincipal user, IMediator mediator, CancellationToken ct) =>
            {
                await mediator.Send(new UnregisterFcmTokenCommand(UserId(user)), ct);
                return Results.NoContent();
            })
            .RequireAuthorization();
        // ctx:push_firebase:end
    }

    private static long UserId(ClaimsPrincipal user) =>
        long.Parse(user.FindFirstValue(SecurityConstants.ClaimTypes.UserId)!);
}
