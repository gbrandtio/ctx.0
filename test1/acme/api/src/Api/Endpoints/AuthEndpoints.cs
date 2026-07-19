using Acme.Application.Abstractions;
using Acme.Application.Security;
using Acme.Domain.Auth;
using Acme.Domain.Entities;
using Acme.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Acme.Api.Endpoints;

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
            RegisterRequest body, AcmeDbContext db, IPasswordHasher hasher, RefreshTokenService tokens) =>
        {
            if (string.IsNullOrWhiteSpace(body.Email) || body.Password.Length < 8)
            {
                return Results.BadRequest(new { error = "Email is required and password must be at least 8 characters." });
            }
            if (await db.Users.AnyAsync(u => u.Email == body.Email))
            {
                return Results.Conflict(new { error = "A user with that email already exists." });
            }

            var user = new User { Email = body.Email };
            db.Users.Add(user);
            db.Set<UserCredential>().Add(new UserCredential { UserId = user.Id, PasswordHash = hasher.Hash(body.Password) });
            await db.SaveChangesAsync();

            return Results.Ok(await tokens.IssueAsync(user.Id));
        });

        group.MapPost("/login", async (
            LoginRequest body, AcmeDbContext db, IPasswordHasher hasher, RefreshTokenService tokens) =>
        {
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == body.Email);
            var credential = user is null ? null : await db.Set<UserCredential>().FindAsync(user.Id);
            if (user is null || credential is null || !hasher.Verify(body.Password, credential.PasswordHash))
            {
                return Results.Json(new { error = "Invalid credentials." }, statusCode: StatusCodes.Status401Unauthorized);
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

        app.MapGet("/v1/me", async (ICurrentUser currentUser, AcmeDbContext db) =>
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
