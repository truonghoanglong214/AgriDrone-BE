namespace AgriDrone.Modules.Missions.Application.Abstractions.Processing;

public interface IGeotagService
{
    Task<GeotagResult> ApplyGeotagAsync(
        string imagesDirectory,
        string csvFilePath,
        double timeOffsetSeconds = 0,
        double maxDiffSeconds = 3.0,
        CancellationToken cancellationToken = default);
}
