using System.Text.Json;
using AgriDrone.Modules.Missions.Application
    .Abstractions.Media;
using AgriDrone.Modules.Missions.Application
    .Abstractions.Missions;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application
    .Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Missions.Application
    .Features.Media.FinalizeMissionUpload;

internal sealed class FinalizeMissionUploadCommandHandler(
    IDroneMissionRepository missionRepository,
    IMissionUploadReadinessQueries readinessQueries,
    IMissionsUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<
        FinalizeMissionUploadCommand,
        Result<FinalizeMissionUploadResult>>
{
    public async Task<Result<FinalizeMissionUploadResult>> Handle(
        FinalizeMissionUploadCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<FinalizeMissionUploadResult>(
                MissionError.CurrentUserRequired());
        }

        var mission = await missionRepository.GetByIdAsync(
            request.MissionId,
            request.TenantId,
            request.FarmId,
            cancellationToken);

        if (mission is null)
        {
            return Result.Failure<FinalizeMissionUploadResult>(
                MissionError.NotFound(request.MissionId));
        }

        if (mission.Status != MissionStatus.Uploading)
        {
            return Result.Failure<FinalizeMissionUploadResult>(
                FinalizeMissionUploadError
                    .MissionStatusNotAllowed(mission.Status));
        }

        if (mission.Version != request.ExpectedMissionVersion)
        {
            return Result.Failure<FinalizeMissionUploadResult>(
                MissionError.VersionConflict(
                    request.ExpectedMissionVersion,
                    mission.Version));
        }

        var now = timeProvider.GetUtcNow();

        var readiness = await readinessQueries.GetAsync(
            request.TenantId,
            request.FarmId,
            request.MissionId,
            now,
            cancellationToken);

        if (readiness.HasActiveUploadSessions)
        {
            return Result.Failure<FinalizeMissionUploadResult>(
                FinalizeMissionUploadError
                    .ActiveUploadSessionsExist());
        }

        var acceptedMediaCount = mission.MissionType switch
        {
            MissionType.Mapping =>
                readiness.RawImageCount,

            MissionType.HealthInspection =>
                readiness.RawImageCount +
                readiness.RawVideoCount,

            _ => 0
        };

        if (acceptedMediaCount < 1)
        {
            return Result.Failure<FinalizeMissionUploadResult>(
                FinalizeMissionUploadError
                    .RequiredMediaMissing(mission.MissionType));
        }

        if (!readiness.HasTelemetryImport ||
            readiness.ImportedPointCount < 2)
        {
            return Result.Failure<FinalizeMissionUploadResult>(
                FinalizeMissionUploadError.TelemetryMissing());
        }

        if (readiness.ImportedPointCount !=
            readiness.PersistedPointCount)
        {
            return Result.Failure<FinalizeMissionUploadResult>(
                FinalizeMissionUploadError.TelemetryInconsistent(
                    readiness.ImportedPointCount,
                    readiness.PersistedPointCount));
        }

        if (mission.FlightRoute is null ||
            mission.FlightRoute.IsEmpty ||
            mission.FlightRoute.NumPoints < 2 ||
            mission.FlightRoute.SRID != 4326)
        {
            return Result.Failure<FinalizeMissionUploadResult>(
                FinalizeMissionUploadError.FlightRouteMissing());
        }

        var previousStatus = mission.Status;

        mission.MarkReadyForProcessing(now);

        AddAudit(
            mission,
            previousStatus,
            acceptedMediaCount,
            readiness.PersistedPointCount,
            actorId,
            now);

        try
        {
            await unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
        catch (MissionConcurrencyException)
        {
            return Result.Failure<FinalizeMissionUploadResult>(
                FinalizeMissionUploadError.ConcurrentUpdate());
        }

        return Result.Success(
            new FinalizeMissionUploadResult(
                mission.Id,
                mission.Status,
                acceptedMediaCount,
                readiness.PersistedPointCount,
                mission.Version));
    }

    private void AddAudit(
        DroneMission mission,
        MissionStatus previousStatus,
        int mediaCount,
        int telemetryPointCount,
        Guid actorId,
        DateTimeOffset finalizedAt)
    {
        using var oldData =
            JsonSerializer.SerializeToDocument(new
            {
                Status = previousStatus.ToString()
            });

        using var newData =
            JsonSerializer.SerializeToDocument(new
            {
                Status = mission.Status.ToString(),
                ProcessingStatus =
                    mission.ProcessingStatus.ToString(),
                MediaCount = mediaCount,
                TelemetryPointCount = telemetryPointCount,
                FlightRouteSrid = mission.FlightRoute!.SRID
            });

        auditWriter.AddUserAction(
            sink: unitOfWork,
            tenantId: mission.TenantId,
            farmId: mission.FarmId,
            actorId: actorId,
            correlationId:
                executionContext.CorrelationId,
            entityType: nameof(DroneMission),
            entityId: mission.Id,
            action: "FINALIZE_MISSION_UPLOAD",
            oldData: oldData,
            newData: newData,
            createdAt: finalizedAt);
    }
}