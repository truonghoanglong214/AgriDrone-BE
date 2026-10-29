using AgriDrone.Modules.Missions.Application.Abstractions.Processing;
using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Processing;

public sealed record SliceMissionTilesCommand(
    Guid TenantId,
    Guid FarmId,
    Guid MissionId,
    int TileSize = 640,
    int Overlap = 64,
    bool DualScale = true,
    object? PolygonGeoJson = null,
    string? TifStorageKey = null,
    bool UploadToMinio = false) : IRequest<Result<SliceTilesResult>>;
