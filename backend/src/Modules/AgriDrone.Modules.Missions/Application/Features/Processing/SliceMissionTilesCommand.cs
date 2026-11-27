using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions.Processing;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Processing;

public sealed record SliceMissionTilesCommand(
    Guid TenantId, Guid FarmId, Guid MissionId, int TileSize, int Overlap, bool DualScale,
    JsonElement? PolygonGeoJson, string? TifStorageKey) : IRequest<Result<MissionTilingJobResponse>>;

public sealed record GetMissionTilingJobQuery(
    Guid TenantId, Guid FarmId, Guid MissionId, Guid? JobId) : IRequest<Result<MissionTilingJobResponse>>;

public sealed record MissionTilingJobResponse(
    Guid JobId, Guid MissionId, string Status, DateTimeOffset CreatedAt, DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt, JsonElement? PolygonGeoJson, SliceTilesResult? Result, string? Error);

internal sealed record MissionTilingInput(
    string SourceKey, string? SourceChecksum, int TileSize, int Overlap, bool DualScale, JsonElement? PolygonGeoJson);
