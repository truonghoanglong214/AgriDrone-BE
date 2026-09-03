namespace AgriDrone.Api.Contracts.Farms;

public sealed record GeoJsonPointResponse(
    string Type,
    double[] Coordinates);
