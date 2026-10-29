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

internal sealed class ProcessMetashapeCommandHandler(
    IDroneMissionRepository missionRepository,
    IMissionsUnitOfWork unitOfWork,
    MissionsDbContext dbContext,
    IMetashapeService metashapeService,
    ISystemManagerAccessService managerAccessService,
    IExecutionContext executionContext,
    TimeProvider timeProvider,
    ILogger<ProcessMetashapeCommandHandler> logger)
    : IRequestHandler<ProcessMetashapeCommand, Result<ProcessMetashapeResult>>
{
    public async Task<Result<ProcessMetashapeResult>> Handle(
        ProcessMetashapeCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is null)
        {
            return Result.Failure<ProcessMetashapeResult>(
                MissionError.CurrentUserRequired());
        }

        var access = await managerAccessService.ResolveFarmAccessAsync(
            request.FarmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId != request.TenantId || access.FarmId != request.FarmId)
        {
            return Result.Failure<ProcessMetashapeResult>(AppError.Forbidden(
                "ProcessMetashape.FarmAccessDenied", "The manager is not assigned to this Farm."));
        }

        var mission = await missionRepository.GetByIdAsync(
            request.MissionId,
            request.TenantId,
            request.FarmId,
            cancellationToken);

        if (mission is null)
        {
            return Result.Failure<ProcessMetashapeResult>(
                MissionError.NotFound(request.MissionId));
        }

        if (mission.Status == MissionStatus.Processing)
        {
            var existingTaskId = ExtractTaskId(mission.Notes);
            var isGhostTask = false;

            if (!string.IsNullOrWhiteSpace(existingTaskId))
            {
                var checkStatus = await metashapeService.GetTaskStatusAsync(existingTaskId, cancellationToken);
                if (!checkStatus.IsSuccess && checkStatus.ErrorMessage is not null &&
                    (checkStatus.ErrorMessage.Contains("NotFound", StringComparison.OrdinalIgnoreCase) ||
                     checkStatus.ErrorMessage.Contains("not found", StringComparison.OrdinalIgnoreCase)))
                {
                    isGhostTask = true;
                    logger.LogWarning(
                        "Mission {MissionId} was locked in Processing by ghost task {TaskId} that no longer exists on engine. Auto-recovering.",
                        request.MissionId,
                        existingTaskId);
                }
            }

            if (!isGhostTask)
            {
                return Result.Failure<ProcessMetashapeResult>(
                    AppError.Conflict(
                        "ProcessMetashape.AlreadyProcessing",
                        "Nhiệm vụ này đang trong quá trình ghép ảnh (PROCESSING). Vui lòng dùng endpoint orthophoto-status để theo dõi tiến độ."));
            }
        }

        if (mission.Status is not (MissionStatus.AwaitingReview or MissionStatus.ReadyForProcessing or MissionStatus.ProcessingFailed))
        {
            return Result.Failure<ProcessMetashapeResult>(
                AppError.Validation(
                    "ProcessMetashape.InvalidStatus",
                    $"Nhiệm vụ ở trạng thái '{mission.Status}' không thể bắt đầu ghép ảnh."));
        }

        var mediaList = await dbContext.MissionMedia
            .Include(mm => mm.Media)
            .AsNoTracking()
            .Where(mm =>
                mm.MissionId == request.MissionId &&
                mm.MediaRole == MissionMediaRole.RawImage)
            .OrderBy(mm => mm.CapturedAt)
            .ThenBy(mm => mm.Media.StorageKey)
            .ThenBy(mm => mm.CreatedAt)
            .ThenBy(mm => mm.MediaId)
            .ToListAsync(cancellationToken);

        if (mediaList.Count == 0)
        {
            return Result.Failure<ProcessMetashapeResult>(
                AppError.Validation(
                    "ProcessMetashape.NoImages",
                    "Mission has no images to process with Metashape."));
        }

        var imageItems = mediaList.Select(mm => new MetashapeTaskImageItem(
            !string.IsNullOrWhiteSpace(mm.Media.StorageKey)
                ? mm.Media.StorageKey
                : $"tenants/{request.TenantId}/media/{request.MissionId}/{mm.MediaId:N}.jpg",
            mm.Media.Url)).ToList();

        logger.LogInformation(
            "Submitting {Count} images to Metashape for Mission {MissionId}...",
            imageItems.Count,
            request.MissionId);

        var taskResult = await metashapeService.CreateTaskAsync(
            request.MissionId,
            request.TenantId,
            request.FarmId,
            imageItems,
            request.CustomOptions,
            request.Engine,
            cancellationToken);

        if (!taskResult.IsSuccess || string.IsNullOrWhiteSpace(taskResult.TaskId))
        {
            logger.LogError(
                "{Engine} task creation failed for Mission {MissionId}: {Error}",
                request.Engine,
                request.MissionId,
                taskResult.ErrorMessage);

            return Result.Failure<ProcessMetashapeResult>(
                AppError.Failure(
                    "ProcessMetashape.CreationFailed",
                    taskResult.ErrorMessage ?? $"Failed to create task in {request.Engine}."));
        }

        var now = timeProvider.GetUtcNow();
        mission.StartMapProcessing(now);
        var engineTag = request.Engine.ToUpperInvariant();
        mission.UpdateNotes($"[{engineTag}] TaskId:{taskResult.TaskId}|StartedAt:{now:O}");

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "{Engine} task {TaskId} successfully initiated for Mission {MissionId}.",
            request.Engine,
            taskResult.TaskId,
            request.MissionId);

        return Result.Success(new ProcessMetashapeResult(
            mission.Id,
            taskResult.TaskId,
            "QUEUED",
            $"{request.Engine} photogrammetry task successfully submitted."));
    }

    private static string? ExtractTaskId(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes)) return null;
        var match = System.Text.RegularExpressions.Regex.Match(notes, @"(?:TaskId:|uuid:)([a-zA-Z0-9\-]+)");
        return match.Success ? match.Groups[1].Value : null;
    }
}
