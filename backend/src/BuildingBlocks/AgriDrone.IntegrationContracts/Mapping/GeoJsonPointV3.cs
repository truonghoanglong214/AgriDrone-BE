namespace AgriDrone.IntegrationContracts.Mapping;

public sealed record GeoJsonPointV3(
    string Type,
    IReadOnlyList<double> Coordinates);
