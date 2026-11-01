using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Application.Abstractions.Processing;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgriDrone.Modules.Missions.Application.Features.Processing;

internal sealed class SliceMissionTilesCommandHandler(
    IDroneMissionRepository missionRepository,
    MissionsDbContext dbContext,
    IMetashapeService metashapeService,
    ISystemManagerAccessService managerAccessService,
    IExecutionContext executionContext,
    ILogger<SliceMissionTilesCommandHandler> logger)
    : IRequestHandler<SliceMissionTilesCommand, Result<SliceTilesResult>>
{
    public async Task<Result<SliceTilesResult>> Handle(
        SliceMissionTilesCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is null)
        {
            return Result.Failure<SliceTilesResult>(MissionError.CurrentUserRequired());
        }

        var access = await managerAccessService.ResolveFarmAccessAsync(
            request.FarmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId != request.TenantId || access.FarmId != request.FarmId)
        {
            return Result.Failure<SliceTilesResult>(AppError.Forbidden(
                "SliceTiles.FarmAccessDenied", "The manager is not assigned to this Farm."));
        }

        var mission = await missionRepository.GetByIdAsync(
            request.MissionId,
            request.TenantId,
            request.FarmId,
            cancellationToken);

        if (mission is null)
        {
            return Result.Failure<SliceTilesResult>(
                MissionError.NotFound(request.MissionId));
        }

        var tifKey = request.TifStorageKey;
        if (string.IsNullOrWhiteSpace(tifKey))
        {
            var tifMedia = await dbContext.MissionMedia
                .Include(mm => mm.Media)
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    mm => mm.MissionId == request.MissionId &&
                          mm.MediaRole == MissionMediaRole.ProcessedImage &&
                          mm.Media.MimeType == "image/tiff",
                    cancellationToken);

            tifKey = tifMedia?.Media?.StorageKey
                ?? $"tenants/{request.TenantId}/media/{request.MissionId}/orthophoto.tif";
        }

        logger.LogInformation(
            "Executing tiling slice for Mission {MissionId} with TIF key {TifKey}...",
            request.MissionId,
            tifKey);

        var result = await metashapeService.SliceTilesAsync(
            request.MissionId,
            request.TenantId,
            tifKey,
            request.TileSize,
            request.Overlap,
            request.DualScale,
            request.PolygonGeoJson,
            request.UploadToMinio,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return Result.Failure<SliceTilesResult>(
                AppError.Failure("SliceTiles.Failed", result.ErrorMessage ?? "Failed to slice orthophoto tiles."));
        }

        return Result.Success(result);
    }
}
