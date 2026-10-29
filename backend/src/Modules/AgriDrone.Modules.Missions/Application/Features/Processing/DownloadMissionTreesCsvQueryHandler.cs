using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Application.Abstractions.Processing;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.Extensions.Logging;

namespace AgriDrone.Modules.Missions.Application.Features.Processing;

internal sealed class DownloadMissionTreesCsvQueryHandler(
    IDroneMissionRepository missionRepository,
    IMetashapeService metashapeService,
    ISystemManagerAccessService managerAccessService,
    IExecutionContext executionContext,
    ILogger<DownloadMissionTreesCsvQueryHandler> logger)
    : IRequestHandler<DownloadMissionTreesCsvQuery, Result<DownloadMissionTreesCsvResult>>
{
    public async Task<Result<DownloadMissionTreesCsvResult>> Handle(
        DownloadMissionTreesCsvQuery request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is null)
        {
            return Result.Failure<DownloadMissionTreesCsvResult>(MissionError.CurrentUserRequired());
        }

        var access = await managerAccessService.ResolveFarmAccessAsync(
            request.FarmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId != request.TenantId || access.FarmId != request.FarmId)
        {
            return Result.Failure<DownloadMissionTreesCsvResult>(AppError.Forbidden(
                "DownloadCsv.FarmAccessDenied", "The manager is not assigned to this Farm."));
        }

        var mission = await missionRepository.GetByIdAsync(
            request.MissionId,
            request.TenantId,
            request.FarmId,
            cancellationToken);

        if (mission is null)
        {
            return Result.Failure<DownloadMissionTreesCsvResult>(
                MissionError.NotFound(request.MissionId));
        }

        logger.LogInformation(
            "Downloading trees CSV for Mission {MissionId}...",
            request.MissionId);

        var csvData = await metashapeService.DownloadTreesCsvAsync(
            request.MissionId,
            cancellationToken);

        if (csvData is null)
        {
            return Result.Failure<DownloadMissionTreesCsvResult>(
                AppError.NotFound("DownloadCsv.NotFound", "Trees CSV not found. Please run YOLO detection first."));
        }

        return Result.Success(new DownloadMissionTreesCsvResult(csvData.Value.Bytes, csvData.Value.FileName));
    }
}
