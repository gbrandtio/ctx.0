using CtxApp.Api.Localization;
using CtxApp.Application.Abstractions;
using CtxApp.Application.Auth;
using CtxApp.Application.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;

namespace CtxApp.Api.Endpoints;

public sealed record RegisterRequest(string Email, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest(string RefreshToken);

/// <summary>
/// Email/password authentication: register and login issue a JWT access token
/// plus a rotating refresh token; refresh rotates the token (with reuse
/// detection); <c>/v1/me</c> returns the authenticated user.
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/auth");

        group.MapPost("/register", async (
            RegisterRequest body,
            IAuthService authService,
            IStringLocalizer<Messages> stringLocalizer,
            CancellationToken cancellationToken) =>
        {
            var result = await authService.RegisterAsync(body.Email, body.Password, cancellationToken);
            if (!result.Success)
            {
                var message = stringLocalizer[$"auth.{result.Error}"].Value;
                return result.Error == "emailTaken"
                    ? Results.Conflict(new { error = message })
                    : Results.BadRequest(new { error = message });
            }
            return Results.Ok(result.Tokens);
        });

        group.MapPost("/login", async (
            LoginRequest body,
            IAuthService authService,
            IStringLocalizer<Messages> stringLocalizer,
            CancellationToken cancellationToken) =>
        {
            var result = await authService.LoginAsync(body.Email, body.Password, cancellationToken);
            if (!result.Success)
            {
                return Results.Json(new { error = stringLocalizer[$"auth.{result.Error}"].Value }, statusCode: StatusCodes.Status401Unauthorized);
            }
            return Results.Ok(result.Tokens);
        });

        group.MapPost("/refresh", async (RefreshRequest body, RefreshTokenService tokens) =>
        {
            try
            {
                return Results.Ok(await tokens.RotateAsync(body.RefreshToken));
            }
            catch (AuthException ex)
            {
                return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status401Unauthorized);
            }
        });

        group.MapPost("/logout", async (RefreshRequest body, RefreshTokenService tokens) =>
        {
            await tokens.RevokeAsync(body.RefreshToken);
            return Results.NoContent();
        });

        app.MapGet("/v1/me", async (ICurrentUser currentUser, IAuthService auth, CancellationToken cancellationToken) =>
            {
                var user = currentUser.UserId is { } id ? await authService.GetUserAsync(id, cancellationToken) : null;
                return user is null
                    ? Results.Unauthorized()
                    : Results.Ok(new { id = user.Id, email = user.Email });
            })
            .RequireAuthorization();

        return app;
    }
}
