using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Application.Abstractions.Processing;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AgriDrone.Modules.Missions.Application.Features.Processing;

internal sealed class DetectMissionTreesCommandHandler(
    IDroneMissionRepository missionRepository,
    IMetashapeService metashapeService,
    ISystemManagerAccessService managerAccessService,
    IExecutionContext executionContext,
    ILogger<DetectMissionTreesCommandHandler> logger)
    : IRequestHandler<DetectMissionTreesCommand, Result<DetectMissionTreesResult>>
{
    public async Task<Result<DetectMissionTreesResult>> Handle(
        DetectMissionTreesCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is null)
        {
            return Result.Failure<DetectMissionTreesResult>(MissionError.CurrentUserRequired());
        }

        var access = await managerAccessService.ResolveFarmAccessAsync(
            request.FarmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId != request.TenantId || access.FarmId != request.FarmId)
        {
            return Result.Failure<DetectMissionTreesResult>(AppError.Forbidden(
                "DetectTrees.FarmAccessDenied", "The manager is not assigned to this Farm."));
        }

        var mission = await missionRepository.GetByIdAsync(
            request.MissionId,
            request.TenantId,
            request.FarmId,
            cancellationToken);

        if (mission is null)
        {
            return Result.Failure<DetectMissionTreesResult>(
                MissionError.NotFound(request.MissionId));
        }

        logger.LogInformation(
            "Executing YOLO tree detection for Mission {MissionId}...",
            request.MissionId);

        var result = await metashapeService.DetectMissionTreesAsync(
            request.MissionId,
            request.TenantId,
            request.ConfidenceThreshold,
            request.RowSpacing,
            request.ColSpacing,
            request.PolygonGeoJson,
            request.AnchorLat,
            request.AnchorLon,
            request.CustomAngleDeg,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return Result.Failure<DetectMissionTreesResult>(
                AppError.Failure("DetectTrees.Failed", result.ErrorMessage ?? "Failed to detect trees."));
        }

        return Result.Success(result);
    }
}
