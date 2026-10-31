using AgriDrone.Modules.Missions.Application.Abstractions.Processing;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Processing;

public sealed record DetectMissionDiseasesCommand(
    Guid TenantId,
    Guid FarmId,
    Guid MissionId,
    double ConfidenceThreshold = 0.25,
    object? PolygonGeoJson = null,
    double? MatchingToleranceMeters = null,
    double? ZoneRadiusMeters = null,
    int? MinDiseasedTrees = null)
    : IRequest<Result<DetectMissionDiseasesResult>>;
