namespace Contracts.Items;

public sealed record ItemResponse(
    long Id,
    string Name,
    string? Description,
    double Latitude,
    double Longitude,
    double DistanceMeters);
