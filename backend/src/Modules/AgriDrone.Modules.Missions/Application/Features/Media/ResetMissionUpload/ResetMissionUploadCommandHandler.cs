using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgriDrone.Modules.Missions.Application.Features.Media.ResetMissionUpload;

internal sealed class ResetMissionUploadCommandHandler(
    IDroneMissionRepository missionRepository,
    MissionsDbContext dbContext,
    IObjectStorage objectStorage,
    IMissionsUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    ISystemManagerAccessService managerAccessService,
    TimeProvider timeProvider,
    ILogger<ResetMissionUploadCommandHandler> logger)
    : IRequestHandler<ResetMissionUploadCommand, Result<ResetMissionUploadResult>>
{
    public async Task<Result<ResetMissionUploadResult>> Handle(
        ResetMissionUploadCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<ResetMissionUploadResult>(
                MissionError.CurrentUserRequired());
        }

        var access = await managerAccessService.ResolveFarmAccessAsync(
            request.FarmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId != request.TenantId ||
            access.FarmId != request.FarmId)
        {
            return Result.Failure<ResetMissionUploadResult>(AppError.Forbidden(
                "MissionUpload.FarmAccessDenied", "The manager is not assigned to this Farm."));
        }

        var mission = await missionRepository.GetByIdAsync(
            request.MissionId,
            request.TenantId,
            request.FarmId,
            cancellationToken);

        if (mission is null)
        {
            return Result.Failure<ResetMissionUploadResult>(
                MissionError.NotFound(request.MissionId));
        }

        if (mission.SurveyOrderId is null)
        {
            return Result.Failure<ResetMissionUploadResult>(
                AppError.Conflict("MissionUpload.MissionNotOrderBound",
                    "Legacy Missions cannot reset uploads."));
        }

        if (mission.Status is not (
            MissionStatus.Uploading or
            MissionStatus.UploadFailed or
            MissionStatus.ReadyForProcessing or
            MissionStatus.ProcessingFailed))
        {
            return Result.Failure<ResetMissionUploadResult>(
                AppError.Conflict(
                    "MissionUpload.CannotResetInCurrentStatus",
                    $"Mission in status '{mission.Status}' cannot reset upload. Only Uploading, UploadFailed, ReadyForProcessing, or ProcessingFailed can be reset."));
        }

        if (request.ExpectedMissionVersion.HasValue &&
            mission.Version != request.ExpectedMissionVersion.Value)
        {
            return Result.Failure<ResetMissionUploadResult>(
                MissionError.VersionConflict(
                    request.ExpectedMissionVersion.Value,
                    mission.Version));
        }

        var now = timeProvider.GetUtcNow();

        // 1. Quét danh sách media và upload sessions để dọn MinIO
        var mediaLinks = await dbContext.MissionMedia
            .Include(mm => mm.Media)
            .Where(mm => mm.MissionId == request.MissionId)
            .ToListAsync(cancellationToken);

        var uploadSessions = await dbContext.MediaUploadSessions
            .Where(s => s.MissionId == request.MissionId)
            .ToListAsync(cancellationToken);

        var storageUris = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var link in mediaLinks)
        {
            if (!string.IsNullOrWhiteSpace(link.Media?.Url))
            {
                storageUris.Add(link.Media.Url);
            }
        }

        foreach (var session in uploadSessions)
        {
            if (!string.IsNullOrWhiteSpace(session.StorageUri))
            {
                storageUris.Add(session.StorageUri);
            }
        }

        // 2. Xóa các object file ảnh trên MinIO
        foreach (var uri in storageUris)
        {
            try
            {
                await objectStorage.DeleteAsync(uri, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to delete storage object {Uri} for mission {MissionId} during upload reset.",
                    uri,
                    request.MissionId);
            }
        }

        // 3. Xóa dữ liệu Media và Upload Session trong DB
        var mediaAssets = mediaLinks
            .Select(l => l.Media)
            .Where(m => m is not null)
            .Distinct()
            .ToList();

        if (mediaLinks.Count > 0)
        {
            dbContext.MissionMedia.RemoveRange(mediaLinks);
        }

        if (mediaAssets.Count > 0)
        {
            dbContext.MediaAssets.RemoveRange(mediaAssets!);
        }

        if (uploadSessions.Count > 0)
        {
            dbContext.MediaUploadSessions.RemoveRange(uploadSessions);
        }

        // 4. Reset Telemetry nếu được yêu cầu
        var telemetryReset = false;
        if (request.ResetTelemetry)
        {
            var points = await dbContext.MissionTelemetryPoints
                .Where(p => p.MissionId == request.MissionId)
                .ToListAsync(cancellationToken);

            var imports = await dbContext.MissionTelemetryImports
                .Where(i => i.MissionId == request.MissionId)
                .ToListAsync(cancellationToken);

            if (points.Count > 0)
            {
                dbContext.MissionTelemetryPoints.RemoveRange(points);
            }

            if (imports.Count > 0)
            {
                dbContext.MissionTelemetryImports.RemoveRange(imports);
            }

            mission.ClearFlightRoute(now);
            telemetryReset = true;
        }

        // 5. Đổi trạng thái Mission về lại Uploading
        var previousStatus = mission.Status;
        mission.ResetUpload(now);

        AddAudit(mission, previousStatus, mediaLinks.Count, telemetryReset, actorId, now);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (MissionConcurrencyException)
        {
            return Result.Failure<ResetMissionUploadResult>(
                AppError.Conflict(
                    "MissionUpload.ConcurrentUpdate",
                    "A concurrent update occurred while resetting the mission upload."));
        }

        return Result.Success(new ResetMissionUploadResult(
            mission.Id,
            mission.Status,
            mediaLinks.Count,
            telemetryReset,
            mission.Version));
    }

    private void AddAudit(
        DroneMission mission,
        MissionStatus previousStatus,
        int deletedMediaCount,
        bool telemetryReset,
        Guid actorId,
        DateTimeOffset changedAt)
    {
        using var oldData = JsonSerializer.SerializeToDocument(new
        {
            Status = previousStatus.ToString()
        });

        using var newData = JsonSerializer.SerializeToDocument(new
        {
            Status = mission.Status.ToString(),
            ProcessingStatus = mission.ProcessingStatus.ToString(),
            DeletedMediaCount = deletedMediaCount,
            TelemetryReset = telemetryReset
        });

        auditWriter.AddUserAction(
            sink: unitOfWork,
            tenantId: mission.TenantId,
            farmId: mission.FarmId,
            actorId: actorId,
            correlationId: executionContext.CorrelationId,
            entityType: nameof(DroneMission),
            entityId: mission.Id,
            action: "RESET_MISSION_UPLOAD",
            oldData: oldData,
            newData: newData,
            createdAt: changedAt);
    }
}

