namespace AgriDrone.IntegrationContracts.Mapping;

public sealed record GeoJsonPolygonV3(
    string Type,
    IReadOnlyList<IReadOnlyList<IReadOnlyList<double>>> Coordinates);
