using NetTopologySuite.Geometries;

namespace Domain.Entities;

/// <summary>
/// Geo-tagged example aggregate (SPATIAL_QUERIES.md). Location maps to
/// PostGIS `geography` so distances are meters, never degrees
/// (ARCHITECTURE_OVERVIEW.md — Domain layer spatial logic).
/// </summary>
public class Item
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Point Location { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
}
