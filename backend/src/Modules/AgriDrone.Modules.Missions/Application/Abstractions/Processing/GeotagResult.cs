namespace AgriDrone.Modules.Missions.Application.Abstractions.Processing;

public sealed record GeotagResult(
    bool IsSuccess,
    int ProcessedCount,
    int ErrorCount,
    string? ErrorMessage);
