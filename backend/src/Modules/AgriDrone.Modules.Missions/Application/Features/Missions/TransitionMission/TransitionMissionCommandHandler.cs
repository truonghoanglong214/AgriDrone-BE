using AgriDrone.Modules.Missions.Application.Abstractions;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Application.Errors;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using System.Text.Json;

namespace AgriDrone.Modules.Missions.Application
    .Features.Missions.TransitionMission;

internal sealed class TransitionMissionCommandHandler(
    IDroneMissionRepository missionRepository,
    IDroneRepository droneRepository,
    IMissionsUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<
        TransitionMissionCommand,
        Result<MissionResponse>>
{
    public async Task<Result<MissionResponse>> Handle(
        TransitionMissionCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<MissionResponse>(
                MissionError.CurrentUserRequired());
        }

        var mission = await missionRepository.GetByIdAsync(
            request.MissionId,
            request.TenantId,
            request.FarmId,
            cancellationToken);

        if (mission is null)
        {
            return Result.Failure<MissionResponse>(
                MissionError.NotFound(request.MissionId));
        }

        if (mission.Version != request.ExpectedVersion)
        {
            return Result.Failure<MissionResponse>(
                MissionError.VersionConflict(
                    request.ExpectedVersion,
                    mission.Version));
        }

        if (!CanTransition(
                mission.Status,
                request.TargetStatus))
        {
            return Result.Failure<MissionResponse>(
                MissionError.InvalidTransition(
                    mission.Status,
                    request.TargetStatus));
        }

        var drone = await droneRepository.GetByIdAsync(
            mission.DroneId,
            mission.TenantId,
            cancellationToken);

        if (drone is null)
        {
            return Result.Failure<MissionResponse>(
                MissionError.DroneNotFound(
                    mission.DroneId));
        }

        var previousMissionStatus = mission.Status;
        var previousDroneStatus = drone.Status;
        var now = timeProvider.GetUtcNow();

        ApplyTransition(
            mission,
            drone,
            request.TargetStatus,
            actorId,
            now);

        AddMissionAudit(
            mission,
            previousMissionStatus,
            request.Reason,
            actorId,
            now);

        if (previousDroneStatus != drone.Status)
        {
            AddDroneAudit(
                drone,
                mission,
                previousDroneStatus,
                actorId,
                now);
        }

        try
        {
            await unitOfWork.SaveChangesAsync(
                cancellationToken);
        }
        catch (MissionConcurrencyException)
        {
            return Result.Failure<MissionResponse>(
                MissionError.ConcurrentUpdate());
        }

        return Result.Success(
            MissionResponseMapper.Map(mission));
    }

    private static bool CanTransition(
        MissionStatus currentStatus,
        MissionStatus targetStatus)
    {
        return (currentStatus, targetStatus) switch
        {
            (MissionStatus.Scheduled,
                MissionStatus.InFlight) => true,

            (MissionStatus.InFlight,
                MissionStatus.FlightCompleted) => true,

            (MissionStatus.InFlight,
                MissionStatus.FlightFailed) => true,

            (MissionStatus.Draft,
                MissionStatus.Cancelled) => true,

            (MissionStatus.Scheduled,
                MissionStatus.Cancelled) => true,

            _ => false
        };
    }

    private static void ApplyTransition(
        DroneMission mission,
        Drone drone,
        MissionStatus targetStatus,
        Guid actorId,
        DateTimeOffset changedAt)
    {
        switch (targetStatus)
        {
            case MissionStatus.InFlight:
                mission.StartFlight(
                    actorId,
                    changedAt);
                drone.StartMission(changedAt);
                break;

            case MissionStatus.FlightCompleted:
                mission.CompleteFlight(changedAt);
                drone.CompleteMission(changedAt);
                break;

            case MissionStatus.FlightFailed:
                mission.FailFlight(changedAt);
                drone.FailMission(changedAt);
                break;

            case MissionStatus.Cancelled:
                mission.Cancel(changedAt);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported target status '{targetStatus}'.");
        }
    }

    private void AddMissionAudit(
        DroneMission mission,
        MissionStatus previousStatus,
        string? reason,
        Guid actorId,
        DateTimeOffset changedAt)
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
                mission.StartedAt,
                mission.EndedAt,
                mission.PreflightConfirmedBy,
                mission.PreflightConfirmedAt,
                Reason = NormalizeReason(reason)
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
            action: GetMissionAction(mission.Status),
            oldData: oldData,
            newData: newData,
            createdAt: changedAt);
    }

    private void AddDroneAudit(
        Drone drone,
        DroneMission mission,
        DroneStatus previousStatus,
        Guid actorId,
        DateTimeOffset changedAt)
    {
        using var oldData =
            JsonSerializer.SerializeToDocument(new
            {
                Status = previousStatus.ToString()
            });

        using var newData =
            JsonSerializer.SerializeToDocument(new
            {
                Status = drone.Status.ToString(),
                MissionId = mission.Id
            });

        auditWriter.AddUserAction(
            sink: unitOfWork,
            tenantId: drone.TenantId,
            farmId: mission.FarmId,
            actorId: actorId,
            correlationId:
                executionContext.CorrelationId,
            entityType: nameof(Drone),
            entityId: drone.Id,
            action: "MISSION_STATUS_CHANGE",
            oldData: oldData,
            newData: newData,
            createdAt: changedAt);
    }

    private static string GetMissionAction(
        MissionStatus status)
    {
        return status switch
        {
            MissionStatus.InFlight =>
                "START_FLIGHT",

            MissionStatus.FlightCompleted =>
                "COMPLETE_FLIGHT",

            MissionStatus.FlightFailed =>
                "FAIL_FLIGHT",

            MissionStatus.Cancelled =>
                "CANCEL",

            _ => "CHANGE_STATUS"
        };
    }

    private static string? NormalizeReason(
        string? reason)
    {
        return string.IsNullOrWhiteSpace(reason)
            ? null
            : reason.Trim();
    }
}
