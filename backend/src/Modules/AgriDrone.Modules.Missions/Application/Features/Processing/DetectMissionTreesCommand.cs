using System.Text.Json;
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
    JsonElement? PolygonGeoJson = null,
    Guid? TilingJobId = null) : IRequest<Result<MissionYoloJobResponse>>;

public sealed record GetMissionYoloJobQuery(
    Guid TenantId, Guid FarmId, Guid MissionId, Guid? JobId) : IRequest<Result<MissionYoloJobResponse>>;

public sealed record MissionYoloJobResponse(
    Guid JobId, Guid MissionId, Guid TilingJobId, string Status, DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt, DateTimeOffset? CompletedAt, DetectMissionTreesResult? Result, string? Error);

internal sealed record MissionYoloInput(
    Guid TilingJobId, string ManifestStorageKey, int TotalTiles, double ConfidenceThreshold, double RowSpacing,
    double ColSpacing, double? AnchorLat, double? AnchorLon, double? CustomAngleDeg, JsonElement? PolygonGeoJson);
