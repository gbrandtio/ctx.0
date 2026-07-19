using CtxApp.Application.Abstractions;
using CtxApp.Domain.Profile;
using CtxApp.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace CtxApp.Api.Endpoints;

public sealed record UpdateProfileRequest(string? DisplayName, string? Bio, string? AvatarUrl, Guid? AvatarMediaId);

/// <summary>
/// The authenticated user's account profile: <c>GET</c> returns it (creating an
/// empty one on first read) and <c>PUT</c> upserts it. Display name and bio are
/// envelope-encrypted; the row is RLS-scoped to the caller. Requires authentication.
/// </summary>
public static class ProfileEndpoints
{
    public static IEndpointRouteBuilder MapProfileEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/profile").RequireAuthorization();

        group.MapGet("/", async (CtxAppDbContext db, ICurrentUser user, CancellationToken ct) =>
        {
            var userId = user.UserId!.Value;
            var profile = await db.Set<UserProfile>().FirstOrDefaultAsync(p => p.UserId == userId, ct);
            if (profile is null)
            {
                profile = new UserProfile { UserId = userId, DisplayName = string.Empty };
                db.Set<UserProfile>().Add(profile);
                await db.SaveChangesAsync(ct);
            }
            return Results.Ok(Present(profile));
        });

        group.MapPut("/", async (UpdateProfileRequest body, CtxAppDbContext db, ICurrentUser user, CancellationToken ct) =>
        {
            var userId = user.UserId!.Value;
            var profile = await db.Set<UserProfile>().FirstOrDefaultAsync(p => p.UserId == userId, ct);
            if (profile is null)
            {
                profile = new UserProfile { UserId = userId, DisplayName = body.DisplayName ?? string.Empty };
                db.Set<UserProfile>().Add(profile);
            }
            else if (body.DisplayName is not null)
            {
                profile.DisplayName = body.DisplayName;
            }

            profile.Bio = body.Bio;
            profile.AvatarUrl = body.AvatarUrl;
            profile.AvatarMediaId = body.AvatarMediaId;
            profile.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            return Results.Ok(Present(profile));
        });

        return app;
    }

    private static object Present(UserProfile p) =>
        new { p.DisplayName, p.Bio, p.AvatarUrl, p.AvatarMediaId, p.UpdatedAt };
}
