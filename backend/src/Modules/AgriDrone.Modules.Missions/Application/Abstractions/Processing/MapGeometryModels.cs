namespace AgriDrone.Modules.Missions.Application.Abstractions.Processing;

public sealed record MapCoordinate(double Latitude, double Longitude);

public sealed record MapBounds(
    double MinLat,
    double MaxLat,
    double MinLon,
    double MaxLon);

public sealed record MapCorners(
    MapCoordinate NorthWest,
    MapCoordinate NorthEast,
    MapCoordinate SouthEast,
    MapCoordinate SouthWest);
