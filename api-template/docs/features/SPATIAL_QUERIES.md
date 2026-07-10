# Spatial Queries & PostGIS

Best practices for storing, indexing, and querying **geo-tagged entities** with PostGIS and NetTopologySuite, entirely code-first. The examples use a generic `Item` entity with a `Location` point — apply the same pattern to any aggregate of yours that carries coordinates.

## 1. Storage: `geography`, not `geometry`

- Coordinates are stored as `NetTopologySuite.Geometries.Point` mapped to the PostGIS **`geography (point)`** column type.
- **Why geography**: distance functions on `geography` return **meters** on the WGS84 spheroid. `geometry` computes in planar degrees which would be wrong for real-world distances.
- **SRID**: always 4326 (WGS84, the GPS coordinate system). Construct points as `new Point(longitude, latitude) { SRID = 4326 }`. Note the **longitude-first** order (X = lon, Y = lat); swapping them is the most common spatial bug.

Setup (DbContext options, extension, column type, GIST index) is covered in the [Database Code-First Guide](../architecture/DATABASE_CODE_FIRST.md) §4.

## 2. Indexing

Every `geography` column gets a **GIST index**, declared in the entity configuration so migrations create it:

```csharp
builder.HasIndex(i => i.Location).HasMethod("GIST");
```

Without the GIST index, radius queries fall back to sequential scans and degrade linearly with row count.

## 3. The Radius-Query Pattern

```csharp
public async Task<List<Item>> GetNearbyAsync(Point userLocation, double radiusMeters)
{
    return await _db.Items
        .AsNoTracking()
        .Where(i => i.Location.IsWithinDistance(userLocation, radiusMeters))
        .OrderBy(i => i.Location.Distance(userLocation))
        .ToListAsync();
}
```

- `IsWithinDistance` translates to `ST_DWithin`, which is **index-accelerated**. Always use it for filtering, never `Distance(x) < r` (which computes the distance for every row before filtering).
- `Distance` translates to `ST_Distance` (meters on `geography`); using it only in `ORDER BY`/projection on the already-filtered set is fine.
- Project the computed distance into the DTO so clients don't recompute it: `.Select(i => new { i, DistanceMeters = i.Location.Distance(userLocation) })`.

## 4. API Surface

- **Endpoint**: `GET /v1/items/nearby?lat=..&lng=..&radiusKm=..` (a `GET` with query parameters so it is output-cacheable. See: [Caching Strategy](../performance/CACHING_STRATEGY.md)).
- **Validation**: clamp `radiusKm` to a documented maximum (e.g., 100 km) to bound query cost; validate lat ∈ [-90, 90], lng ∈ [-180, 180].
- **Response**: paginated list sorted by distance, with `distanceMeters` per item.

## 5. Rules

1. Never store lat/lng as bare `double` columns. Always the spatial type, so indexing and distance semantics come for free.
2. Never compare distances in degrees; if you see a radius like `0.009` in code, it's a bug.
3. Filtering must go through `IsWithinDistance` (`ST_DWithin`).
4. Radius-search endpoints are public-cacheable reads: `GET`, `AsNoTracking()`, output-cached with query-string vary.
