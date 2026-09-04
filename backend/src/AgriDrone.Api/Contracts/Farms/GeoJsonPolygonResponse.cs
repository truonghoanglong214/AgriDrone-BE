namespace AgriDrone.Api.Contracts.Farms;

public sealed record GeoJsonPolygonResponse(
    string Type,
    double[][][] Coordinates);
