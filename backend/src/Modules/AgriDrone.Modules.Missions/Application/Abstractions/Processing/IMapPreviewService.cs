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

public sealed record MapPreviewResult(
    bool IsSuccess,
    string? PreviewPath,
    int OriginalWidth,
    int OriginalHeight,
    int PreviewWidth,
    int PreviewHeight,
    MapBounds? Bounds,
    MapCorners? Corners,
    string? ErrorMessage);

public interface IMapPreviewService
{
    Task<MapPreviewResult> GeneratePreviewAsync(
        string inputTifPath,
        string outputJpgPath,
        int maxSize = 2048,
        int quality = 85,
        CancellationToken cancellationToken = default);
}
