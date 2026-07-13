using Application.Abstractions;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace Infrastructure.Repositories.Items;

public sealed class ItemRepository(AppDbContext db) : IItemRepository
{
    /// <summary>
    /// ST_DWithin over geography via IsWithinDistance — meters, GIST-index
    /// assisted (SPATIAL_QUERIES.md). Never compare distances in degrees.
    /// </summary>
    public async Task<IReadOnlyList<(Item Item, double DistanceMeters)>> GetNearbyAsync(
        double latitude, double longitude, double radiusMeters, CancellationToken ct)
    {
        var origin = new Point(longitude, latitude) { SRID = 4326 };
        var results = await db.Items
            .Where(i => i.Location.IsWithinDistance(origin, radiusMeters))
            .Select(i => new { Item = i, Distance = i.Location.Distance(origin) })
            .OrderBy(x => x.Distance)
            .Take(100)
            .AsNoTracking()
            .ToListAsync(ct);
        return [.. results.Select(x => (x.Item, x.Distance))];
    }
}
