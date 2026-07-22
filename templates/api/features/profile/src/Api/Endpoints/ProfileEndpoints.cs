using CtxApp.Application.Abstractions;
using CtxApp.Domain.Profile;
using CtxApp.Application.Profile;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

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

        group.MapGet("/", async (IProfileService profileService, ICurrentUser user, CancellationToken ct) =>
        {
            var profile = await profileService.GetOrCreateProfileAsync(user.UserId!.Value, ct);
            return Results.Ok(profile);
        });

        group.MapPut("/", async (UpdateProfileRequest body, IProfileService profileService, ICurrentUser user, CancellationToken ct) =>
        {
            var profile = await profileService.UpdateProfileAsync(
                user.UserId!.Value, 
                body.DisplayName, 
                body.Bio, 
                body.AvatarUrl, 
                body.AvatarMediaId, 
                ct);

            return Results.Ok(profile);
        });

        return app;
    }
}
