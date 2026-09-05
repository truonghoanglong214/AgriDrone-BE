using AgriDrone.Api.Contracts.Farms;

namespace AgriDrone.Api.Contracts.Zones;

public sealed record CreateZoneRequest(
    string Code,
    string Name,
    GeoJsonPolygonRequest? Boundary,
    decimal? AreaHectares);
