using AgriDrone.Modules.Missions.Application.Abstractions.Media;
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

internal sealed class ProcessNodeOdmCommandHandler(
    IDroneMissionRepository missionRepository,
    IMissionsUnitOfWork unitOfWork,
    MissionsDbContext dbContext,
    IObjectStorage objectStorage,
    INodeOdmService nodeOdmService,
    ISystemManagerAccessService managerAccessService,
    IExecutionContext executionContext,
    TimeProvider timeProvider,
    ILogger<ProcessNodeOdmCommandHandler> logger)
    : IRequestHandler<ProcessNodeOdmCommand, Result<ProcessNodeOdmResult>>
{
    public async Task<Result<ProcessNodeOdmResult>> Handle(
        ProcessNodeOdmCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is null)
        {
            return Result.Failure<ProcessNodeOdmResult>(
                MissionError.CurrentUserRequired());
        }

        var access = await managerAccessService.ResolveFarmAccessAsync(
            request.FarmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId != request.TenantId || access.FarmId != request.FarmId)
        {
            return Result.Failure<ProcessNodeOdmResult>(AppError.Forbidden(
                "ProcessNodeOdm.FarmAccessDenied", "The manager is not assigned to this Farm."));
        }

        var mission = await missionRepository.GetByIdAsync(
            request.MissionId,
            request.TenantId,
            request.FarmId,
            cancellationToken);

        if (mission is null)
        {
            return Result.Failure<ProcessNodeOdmResult>(
                MissionError.NotFound(request.MissionId));
        }

        var mediaList = await dbContext.MissionMedia
            .Include(mm => mm.Media)
            .AsNoTracking()
            .Where(mm =>
                mm.MissionId == request.MissionId &&
                mm.MediaRole == MissionMediaRole.RawImage)
            .ToListAsync(cancellationToken);

        if (mediaList.Count == 0)
        {
            return Result.Failure<ProcessNodeOdmResult>(
                AppError.Validation(
                    "ProcessNodeOdm.NoImages",
                    "Mission has no images to process with NodeODM."));
        }

        var tempDir = Path.Combine(
            Path.GetTempPath(),
            "agridrone",
            "nodeodm_upload",
            request.MissionId.ToString("N"));

        try
        {
            Directory.CreateDirectory(tempDir);

            logger.LogInformation(
                "Downloading {Count} geotagged images for Mission {MissionId} to submit to NodeODM...",
                mediaList.Count,
                request.MissionId);

            var downloadedFiles = new List<string>();
            foreach (var mediaItem in mediaList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fileName = $"{mediaItem.MediaId:N}.jpg";
                var localFilePath = Path.Combine(tempDir, fileName);

                await objectStorage.ReadAsync(
                    mediaItem.Media.Url,
                    async (stream, ct) =>
                    {
                        await using var fs = File.Create(localFilePath);
                        await stream.CopyToAsync(fs, ct);
                    },
                    cancellationToken);

                downloadedFiles.Add(localFilePath);
            }

            logger.LogInformation(
                "Submitting {Count} images to NodeODM for Mission {MissionId}...",
                downloadedFiles.Count,
                request.MissionId);

            var taskResult = await nodeOdmService.CreateTaskAsync(
                downloadedFiles,
                request.CustomOptions,
                cancellationToken);

            if (!taskResult.IsSuccess || string.IsNullOrWhiteSpace(taskResult.TaskId))
            {
                logger.LogError(
                    "NodeODM task creation failed for Mission {MissionId}: {Error}",
                    request.MissionId,
                    taskResult.ErrorMessage);

                return Result.Failure<ProcessNodeOdmResult>(
                    AppError.Failure(
                        "ProcessNodeOdm.CreationFailed",
                        taskResult.ErrorMessage ?? "Failed to create task in NodeODM."));
            }

            var now = timeProvider.GetUtcNow();
            mission.StartMapProcessing(now);
            mission.UpdateNotes($"[NODEODM] TaskId:{taskResult.TaskId}|StartedAt:{now:O}");

            await unitOfWork.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "NodeODM task {TaskId} successfully initiated for Mission {MissionId}.",
                taskResult.TaskId,
                request.MissionId);

            return Result.Success(new ProcessNodeOdmResult(
                mission.Id,
                taskResult.TaskId,
                "QUEUED",
                "NodeODM task successfully submitted."));
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to clean up NodeODM upload temp directory {Dir}", tempDir);
            }
        }
    }
}
