using AgriDrone.Modules.Missions.Application.Abstractions.Processing;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Processing;

public sealed record DetectMissionTreesCommand(
    Guid TenantId,
    Guid FarmId,
    Guid MissionId,
    double ConfidenceThreshold = 0.25,
    double RowSpacing = 2.8,
    double ColSpacing = 2.5,
    double? AnchorLat = null,
    double? AnchorLon = null,
    double? CustomAngleDeg = null,
    object? PolygonGeoJson = null) : IRequest<Result<DetectMissionTreesResult>>;
