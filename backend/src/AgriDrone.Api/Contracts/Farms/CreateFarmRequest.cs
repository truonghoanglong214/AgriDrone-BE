namespace AgriDrone.Api.Contracts.Farms;

public sealed record CreateFarmRequest(
    string Code,
    string Name,
    string? Address,
    GeoJsonPolygonRequest? Boundary,
    GeoJsonPointRequest? CenterPoint,
    decimal? AreaHectares);
