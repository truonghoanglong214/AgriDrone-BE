using AgriDrone.Api.Contracts.Farms;

namespace AgriDrone.Api.Contracts.Zones;

public sealed record UpdateZoneRequest(
    string Name,
    GeoJsonPolygonRequest? Boundary,
    decimal? AreaHectares,
    long ExpectedVersion);
