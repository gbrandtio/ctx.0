using AppApi.Endpoints;
using Application.Features.Items;
using MediatR;

namespace AppApi.Endpoints.v1;

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
