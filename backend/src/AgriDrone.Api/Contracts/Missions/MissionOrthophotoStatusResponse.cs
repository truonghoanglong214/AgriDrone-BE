using AgriDrone.Modules.Missions.Application.Abstractions.Processing;

namespace AgriDrone.Api.Contracts.Missions;

public sealed record MissionOrthophotoStatusResponse(
    Guid MissionId,
    string? TaskId,
    string Status,
    int Progress,
    long ProcessingTimeMs,
    string? PreviewUrl,
    string? TifUrl,
    MapBounds? Bounds,
    MapCorners? Corners,
    string? ErrorMessage);
