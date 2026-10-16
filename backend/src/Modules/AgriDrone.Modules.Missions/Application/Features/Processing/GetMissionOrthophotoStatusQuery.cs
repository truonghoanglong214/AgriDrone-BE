using AgriDrone.Modules.Missions.Application.Abstractions.Processing;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Processing;

public sealed record GetMissionOrthophotoStatusQuery(
    Guid TenantId,
    Guid FarmId,
    Guid MissionId)
    : IRequest<Result<MissionOrthophotoStatusResult>>;

public sealed record MissionOrthophotoStatusResult(
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
