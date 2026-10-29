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

internal sealed class DetectMissionDiseasesCommandHandler(
    IDroneMissionRepository missionRepository,
    MissionsDbContext dbContext,
    IMetashapeService metashapeService,
    ISystemManagerAccessService managerAccessService,
    IExecutionContext executionContext,
    ILogger<DetectMissionDiseasesCommandHandler> logger)
    : IRequestHandler<DetectMissionDiseasesCommand, Result<DetectMissionDiseasesResult>>
{
    public async Task<Result<DetectMissionDiseasesResult>> Handle(
        DetectMissionDiseasesCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is null)
        {
            return Result.Failure<DetectMissionDiseasesResult>(MissionError.CurrentUserRequired());
        }

        var access = await managerAccessService.ResolveFarmAccessAsync(
            request.FarmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId != request.TenantId || access.FarmId != request.FarmId)
        {
            return Result.Failure<DetectMissionDiseasesResult>(AppError.Forbidden(
                "DetectDiseases.FarmAccessDenied", "The manager is not assigned to this Farm."));
        }

        var mission = await missionRepository.GetByIdAsync(
            request.MissionId,
            request.TenantId,
            request.FarmId,
            cancellationToken);

        if (mission is null)
        {
            return Result.Failure<DetectMissionDiseasesResult>(
                MissionError.NotFound(request.MissionId));
        }

        // Truy vấn danh sách ảnh thô đã gắn GPS của Mission
        var mediaList = await dbContext.MissionMedia
            .Include(mm => mm.Media)
            .AsNoTracking()
            .Where(mm =>
                mm.MissionId == request.MissionId &&
                mm.MediaRole == MissionMediaRole.RawImage)
            .OrderBy(mm => mm.CapturedAt)
            .ThenBy(mm => mm.Media.StorageKey)
            .ToListAsync(cancellationToken);

        List<MetashapeTaskImageItem>? imageItems = null;
        if (mediaList.Count > 0)
        {
            imageItems = mediaList.Select(mm => new MetashapeTaskImageItem(
                !string.IsNullOrWhiteSpace(mm.Media.StorageKey)
                    ? mm.Media.StorageKey
                    : $"tenants/{request.TenantId}/media/{request.MissionId}/{mm.MediaId:N}.jpg",
                mm.Media.Url)).ToList();
        }

        logger.LogInformation(
            "Executing disease detection for Mission {MissionId} with {Count} images...",
            request.MissionId,
            imageItems?.Count ?? 0);

        var result = await metashapeService.DetectMissionDiseasesAsync(
            request.MissionId,
            request.TenantId,
            imageItems,
            request.ConfidenceThreshold,
            request.PolygonGeoJson,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return Result.Failure<DetectMissionDiseasesResult>(
                AppError.Failure("DetectDiseases.Failed", result.ErrorMessage ?? "Failed to detect diseases."));
        }

        return Result.Success(result);
    }
}
