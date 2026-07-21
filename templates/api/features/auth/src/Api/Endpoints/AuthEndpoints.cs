using CtxApp.Api.Localization;
using CtxApp.Application.Abstractions;
using CtxApp.Application.Security;
using CtxApp.Domain.Auth;
using CtxApp.Domain.Entities;
using CtxApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
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
            CtxAppDbContext db,
            IPasswordHasher hasher,
            RefreshTokenService tokens,
            IStringLocalizer<Messages> loc) =>
        {
            if (string.IsNullOrWhiteSpace(body.Email) || body.Password.Length < 8)
            {
                return Results.BadRequest(new { error = loc["auth.credentialsRequired"].Value });
            }
            if (await db.Users.AnyAsync(u => u.Email == body.Email))
            {
                return Results.Conflict(new { error = loc["auth.emailTaken"].Value });
            }

            var user = new User { Email = body.Email };
            db.Users.Add(user);
            db.Set<UserCredential>().Add(new UserCredential { UserId = user.Id, PasswordHash = hasher.Hash(body.Password) });
            await db.SaveChangesAsync();

            return Results.Ok(await tokens.IssueAsync(user.Id));
        });

        group.MapPost("/login", async (
            LoginRequest body,
            CtxAppDbContext db,
            IPasswordHasher hasher,
            RefreshTokenService tokens,
            IStringLocalizer<Messages> loc) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == body.Email);
            var credential = user is null ? null : await db.Set<UserCredential>().FindAsync(user.Id);
            if (user is null || credential is null || !hasher.Verify(body.Password, credential.PasswordHash))
            {
                return Results.Json(new { error = loc["auth.invalidCredentials"].Value }, statusCode: StatusCodes.Status401Unauthorized);
            }
            return Results.Ok(await tokens.IssueAsync(user.Id));
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

        app.MapGet("/v1/me", async (ICurrentUser currentUser, CtxAppDbContext db) =>
            {
                var user = currentUser.UserId is { } id ? await db.Users.FindAsync(id) : null;
                return user is null
                    ? Results.Unauthorized()
                    : Results.Ok(new { id = user.Id, email = user.Email });
            })
            .RequireAuthorization();

        return app;
    }
}
