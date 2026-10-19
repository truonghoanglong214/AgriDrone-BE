namespace AgriDrone.Api.Contracts.Farms;

public sealed record UpdateFarmDetailRequest(
    string Name,
    string? Address,
    GeoJsonPointRequest? CenterPoint,
    decimal? AreaHectares,
    long ExpectedVersion);
