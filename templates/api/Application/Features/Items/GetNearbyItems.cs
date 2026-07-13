using Application.Abstractions;
using Contracts.Items;
using Domain.Exceptions;
using MediatR;

namespace Application.Features.Items;

public sealed record GetNearbyItemsQuery(double Latitude, double Longitude, double RadiusKm)
    : IRequest<IReadOnlyList<ItemResponse>>;

/// <summary>
/// Nearby geo-tagged items (SPATIAL_QUERIES.md): validates coordinates,
/// clamps the radius to bound query cost, and queries in meters via
/// PostGIS geography.
/// </summary>
public sealed class GetNearbyItemsHandler(IItemRepository items)
    : IRequestHandler<GetNearbyItemsQuery, IReadOnlyList<ItemResponse>>
{
    public const double MaxRadiusKm = 100;

    public async Task<IReadOnlyList<ItemResponse>> Handle(
        GetNearbyItemsQuery query, CancellationToken ct)
    {
        if (query.Latitude is < -90 or > 90 || query.Longitude is < -180 or > 180)
        {
            throw new DomainException("Invalid coordinates.");
        }

        var radiusMeters = Math.Clamp(query.RadiusKm, 0.1, MaxRadiusKm) * 1000;
        var results = await items.GetNearbyAsync(
            query.Latitude, query.Longitude, radiusMeters, ct);

        return [.. results.Select(r => new ItemResponse(
            r.Item.Id, r.Item.Name, r.Item.Description,
            r.Item.Location.Y, r.Item.Location.X, r.DistanceMeters))];
    }
}
