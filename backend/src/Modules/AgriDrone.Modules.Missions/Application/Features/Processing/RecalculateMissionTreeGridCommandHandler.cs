using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Application.Abstractions.Processing;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AgriDrone.Modules.Missions.Application.Features.Processing;

internal sealed class RecalculateMissionTreeGridCommandHandler(
    IDroneMissionRepository missionRepository,
    IMetashapeService metashapeService,
    ISystemManagerAccessService managerAccessService,
    IExecutionContext executionContext,
    ILogger<RecalculateMissionTreeGridCommandHandler> logger)
    : IRequestHandler<RecalculateMissionTreeGridCommand, Result<DetectMissionTreesResult>>
{
    public async Task<Result<DetectMissionTreesResult>> Handle(
        RecalculateMissionTreeGridCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is null)
        {
            return Result.Failure<DetectMissionTreesResult>(MissionError.CurrentUserRequired());
        }

        if (request.ActiveTrees.Count == 0)
        {
            return Result.Failure<DetectMissionTreesResult>(AppError.Validation(
                "RecalculateTreeGrid.EmptyTrees", "Danh sách cây rỗng, không thể tính toán lại."));
        }

        var access = await managerAccessService.ResolveFarmAccessAsync(
            request.FarmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId != request.TenantId || access.FarmId != request.FarmId)
        {
            return Result.Failure<DetectMissionTreesResult>(AppError.Forbidden(
                "RecalculateTreeGrid.FarmAccessDenied", "The manager is not assigned to this Farm."));
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
            "Recalculating tree grid (rows/cols) for Mission {MissionId} with {TreeCount} trees...",
            request.MissionId,
            request.ActiveTrees.Count);

        var result = await metashapeService.RecalculateTreeGridAsync(
            request.MissionId,
            request.ActiveTrees,
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
                AppError.Failure("RecalculateTreeGrid.Failed", result.ErrorMessage ?? "Failed to recalculate tree grid."));
        }

        return Result.Success(result);
    }
}
