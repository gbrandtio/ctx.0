using AppApi.Endpoints;
using Application.Features.Items;
using MediatR;
using Microsoft.AspNetCore.OutputCaching;

namespace AppApi.Endpoints.v1;

/// <summary>
/// Output-cache policy for /items/nearby (M11). The built-in default policy
/// refuses to cache any request carrying an Authorization header, and this
/// endpoint requires auth, so it would never cache. Nearby items are not
/// user-specific, so this policy enables caching regardless of the auth
/// header, keyed only by the spatial query parameters. It must set the
/// Allow* knobs itself because those are otherwise only set by the default
/// policy we are deliberately bypassing.
/// </summary>
public sealed class ItemsNearbyCachePolicy : IOutputCachePolicy
{
    private static readonly string[] QueryKeys = ["lat", "lng", "radiusKm"];

    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken ct)
    {
        context.EnableOutputCaching = true;
        context.AllowCacheLookup = true;
        context.AllowCacheStorage = true;
        context.AllowLocking = true;
        context.ResponseExpirationTimeSpan = TimeSpan.FromSeconds(30);
        context.CacheVaryByRules.QueryKeys = QueryKeys;
        context.Tags.Add("items");
        return ValueTask.CompletedTask;
    }

    public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken ct) =>
        ValueTask.CompletedTask;

    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken ct)
    {
        // Only cache successful responses.
        if (context.HttpContext.Response.StatusCode != StatusCodes.Status200OK)
        {
            context.AllowCacheStorage = false;
        }
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Nearby geo-tagged items (SPATIAL_QUERIES.md): a GET with query
/// parameters, output-cached for 30s (CACHING_STRATEGY.md).
/// </summary>
public sealed class ItemsEndpoints : IEndpointModule
{
    public void Map(IEndpointRouteBuilder v1)
    {
        v1.MapGet("/items/nearby", async (
                double lat, double lng, double radiusKm,
                IMediator mediator, CancellationToken ct) =>
                Results.Ok(await mediator.Send(
                    new GetNearbyItemsQuery(lat, lng, radiusKm == 0 ? 10 : radiusKm), ct)))
            .RequireAuthorization()
            .CacheOutput("items-nearby");
    }
}
