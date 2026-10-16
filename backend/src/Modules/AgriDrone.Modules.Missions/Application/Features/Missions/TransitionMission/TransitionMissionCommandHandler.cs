using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions;
using AgriDrone.Modules.Missions.Application.Abstractions.MissionPlanning;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.TransitionMission;

internal sealed class TransitionMissionCommandHandler(
    IDroneMissionRepository missionRepository,
    IDroneRepository droneRepository,
    IDroneMaintenanceRepository maintenanceRepository,
    IPreflightChecklistRepository checklistRepository,
    IMissionFieldNoteRepository fieldNoteRepository,
    ISystemManagerAccessService managerAccessService,
    ISurveyOrderMissionPlanningQuery orderQuery,
    IMissionsUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<TransitionMissionCommand, Result<MissionResponse>>
{
    public async Task<Result<MissionResponse>> Handle(
        TransitionMissionCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
            return Result.Failure<MissionResponse>(MissionError.CurrentUserRequired());

        var access = await managerAccessService.ResolveFarmAccessAsync(
            request.FarmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId is not Guid tenantId ||
            access.FarmId != request.FarmId)
        {
            return Result.Failure<MissionResponse>(
                MissionOperationError.FarmAccessDenied());
        }

        var mission = await missionRepository.GetByIdAsync(
            request.MissionId, tenantId, request.FarmId, cancellationToken);
        if (mission is null)
            return Result.Failure<MissionResponse>(MissionError.NotFound(request.MissionId));
        if (mission.SurveyOrderId is not Guid surveyOrderId)
            return Result.Failure<MissionResponse>(MissionOperationError.MissionNotOrderBound());
        if (mission.Purpose is null)
            return Result.Failure<MissionResponse>(MissionOperationError.InvalidOrderContext());
        if (mission.Version != request.ExpectedVersion)
        {
            return Result.Failure<MissionResponse>(
                MissionError.VersionConflict(request.ExpectedVersion, mission.Version));
        }
        if (!CanTransition(mission.Status, request.TargetStatus))
        {
            return Result.Failure<MissionResponse>(
                MissionError.InvalidTransition(mission.Status, request.TargetStatus));
        }
        if (request.TargetStatus is MissionStatus.FlightFailed or MissionStatus.Cancelled &&
            string.IsNullOrWhiteSpace(request.Reason))
            return Result.Failure<MissionResponse>(
                MissionOperationError.ReasonRequired());
        if (request.TargetStatus == MissionStatus.FlightFailed &&
            !HasValidFailureIncident(request))
            return Result.Failure<MissionResponse>(
                MissionOperationError.FailureIncidentRequired());

        var drone = await droneRepository.GetByIdAsync(mission.DroneId, cancellationToken);
        if (drone is null)
            return Result.Failure<MissionResponse>(MissionError.DroneNotFound(mission.DroneId));

        var now = timeProvider.GetUtcNow();
        if (request.TargetStatus == MissionStatus.InFlight)
        {
            var readinessError = await ValidateStartReadinessAsync(
                mission, drone, surveyOrderId, access.ManagerProfileId, actorId, now,
                cancellationToken);
            if (readinessError is not null)
                return Result.Failure<MissionResponse>(readinessError);
        }

        var previousMissionStatus = mission.Status;
        var previousDroneStatus = drone.Status;
        ApplyTransition(mission, drone, request.TargetStatus, actorId, now);
        if (request.TargetStatus == MissionStatus.FlightFailed)
        {
            fieldNoteRepository.Add(MissionFieldNote.Create(
                mission.TenantId, mission.FarmId, mission.Id,
                request.IncidentOperationId!.Value, actorId,
                request.Reason!, now, now, request.IncidentType,
                request.IncidentOutcome, request.RecoveryDecision,
                request.EvidenceReference));
            maintenanceRepository.Add(DroneMaintenanceRecord.Start(
                drone.Id, now, actorId,
                string.IsNullOrWhiteSpace(request.Reason)
                    ? "FLIGHT_FAILURE"
                    : request.Reason.Trim()));
        }
        AddMissionAudit(mission, previousMissionStatus, request, actorId, now);
        if (previousDroneStatus != drone.Status)
            AddDroneAudit(drone, mission, previousDroneStatus, actorId, now);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (MissionConcurrencyException)
        {
            return Result.Failure<MissionResponse>(MissionError.ConcurrentUpdate());
        }
        catch (DroneConcurrencyException)
        {
            return Result.Failure<MissionResponse>(MissionError.ConcurrentUpdate());
        }
        catch (DroneMaintenanceConflictException)
        {
            return Result.Failure<MissionResponse>(MissionError.ConcurrentUpdate());
        }
        catch (MissionFieldNoteConflictException)
        {
            return Result.Failure<MissionResponse>(MissionError.ConcurrentUpdate());
        }

        return Result.Success(MissionResponseMapper.Map(mission));
    }

    private async Task<AppError?> ValidateStartReadinessAsync(
        DroneMission mission,
        Drone drone,
        Guid surveyOrderId,
        Guid? managerProfileId,
        Guid actorId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        SurveyOrderMissionPlanningContext? order;
        try
        {
            order = await orderQuery.GetForPurposeAsync(
                surveyOrderId, mission.Purpose!.Value, cancellationToken);
        }
        catch (SurveyOrderMissionPlanningUnavailableException)
        {
            return MissionOperationError.OrderContextUnavailable();
        }

        if (order is null)
            return MissionOperationError.OrderNotFound(surveyOrderId);
        if (order.SurveyOrderId != surveyOrderId || order.TenantId != mission.TenantId ||
            order.FarmId != mission.FarmId)
            return MissionOperationError.InvalidOrderContext();
        if (mission.Purpose == MissionPurpose.PlantHealth &&
            order.SelectedService != SurveyServiceType.PlantHealth ||
            mission.Purpose == MissionPurpose.HarvestReadiness &&
            order.SelectedService != SurveyServiceType.HarvestReadiness)
            return MissionOperationError.InvalidOrderContext();
        if (order.FarmBoundaryVersionId is null ||
            order.FarmBoundaryVersionId != mission.FarmBoundaryVersionId ||
            order.ScopeZoneIds is null ||
            !order.ScopeZoneIds.Order().SequenceEqual(mission.ScopeZoneIds.Order()) ||
            managerProfileId is null ||
            order.PrimarySystemManagerId != managerProfileId)
            return MissionOperationError.InvalidOrderContext();
        if (!order.IsReadyForOperations || !order.IsReadyToSchedule)
            return MissionOperationError.OrderNotReady(order.ReadinessFailureCode);

        if (mission.ScheduledAt is not DateTimeOffset scheduledAt ||
            mission.ScheduledEndAt is not DateTimeOffset scheduledEndAt ||
            now < scheduledAt || now >= scheduledEndAt)
            return MissionOperationError.ScheduleWindowClosed();
        if (now < order.AppointmentStartAt || now >= order.AppointmentEndAt)
            return MissionOperationError.AppointmentWindowClosed();
        if (mission.PreflightConfirmedBy is null || mission.PreflightConfirmedAt is null ||
            mission.PreflightSuitableForFlight != true ||
            mission.PreflightConfirmedBy != actorId)
            return MissionOperationError.PreflightRequired();
        var currentChecklist = await checklistRepository.GetActiveDefinitionAsync(
            "DRONE_PRE_FLIGHT", cancellationToken);
        if (currentChecklist is null ||
            !string.Equals(mission.PreflightChecklistVersion,
                $"v{currentChecklist.VersionNumber}", StringComparison.Ordinal))
            return MissionOperationError.PreflightStale();
        var completedChecklist = mission.PreflightOperationId is Guid operationId
            ? await checklistRepository.GetByOperationIdAsync(
                mission.Id, operationId, cancellationToken)
            : null;
        if (completedChecklist is null ||
            completedChecklist.Status != MissionPreflightChecklistStatus.Completed ||
            completedChecklist.ChecklistDefinitionId != currentChecklist.Id ||
            completedChecklist.CompletedBy != mission.PreflightConfirmedBy ||
            completedChecklist.Responses.RootElement.EnumerateObject().Any(answer =>
                answer.Value.ValueKind == JsonValueKind.False) ||
            string.IsNullOrWhiteSpace(completedChecklist.FlightAuthorizationEvidence) ||
            string.IsNullOrWhiteSpace(completedChecklist.FailsafeNotes))
            return MissionOperationError.SafetyEvidenceRequired();
        if (mission.RequiresBaselineCompletion && mission.SourceMapVersionId is null)
            return MissionOperationError.BaselineNotCompleted();
        if (mission.Purpose is MissionPurpose.PlantHealth or MissionPurpose.HarvestReadiness &&
            (mission.SourceMapVersionId is null ||
             mission.SourceMapVersionId != order.CurrentBaseMapVersionId))
            return MissionOperationError.BaselineNotCompleted();

        if (!drone.IsOperationalFor(scheduledAt, scheduledEndAt) ||
            !drone.SupportsPurpose(mission.Purpose!.Value))
            return MissionOperationError.DroneNotOperational(drone.Id);

        return null;
    }

    private static bool CanTransition(MissionStatus current, MissionStatus target) =>
        (current, target) is
            (MissionStatus.Scheduled, MissionStatus.InFlight) or
            (MissionStatus.InFlight, MissionStatus.FlightCompleted) or
            (MissionStatus.InFlight, MissionStatus.FlightFailed) or
            (MissionStatus.Draft, MissionStatus.Cancelled) or
            (MissionStatus.Scheduled, MissionStatus.Cancelled);

    private static void ApplyTransition(
        DroneMission mission, Drone drone, MissionStatus target, Guid actorId,
        DateTimeOffset changedAt)
    {
        switch (target)
        {
            case MissionStatus.InFlight:
                mission.StartFlight(actorId, changedAt);
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
                throw new InvalidOperationException($"Unsupported target status '{target}'.");
        }
    }

    private void AddMissionAudit(
        DroneMission mission, MissionStatus previousStatus, TransitionMissionCommand request,
        Guid actorId, DateTimeOffset changedAt)
    {
        using var oldData = JsonSerializer.SerializeToDocument(new
        {
            Status = previousStatus.ToString()
        });
        using var newData = JsonSerializer.SerializeToDocument(new
        {
            Status = mission.Status.ToString(),
            mission.StartedAt,
            mission.EndedAt,
            mission.PreflightConfirmedBy,
            mission.PreflightConfirmedAt,
            Reason = NormalizeReason(request.Reason),
            request.IncidentOperationId,
            request.IncidentType,
            request.IncidentOutcome,
            request.RecoveryDecision,
            request.EvidenceReference
        });
        auditWriter.AddUserAction(unitOfWork, mission.TenantId, mission.FarmId,
            actorId, executionContext.CorrelationId, nameof(DroneMission), mission.Id,
            GetMissionAction(mission.Status), oldData, newData, changedAt);
    }

    private void AddDroneAudit(
        Drone drone, DroneMission mission, DroneStatus previousStatus,
        Guid actorId, DateTimeOffset changedAt)
    {
        using var oldData = JsonSerializer.SerializeToDocument(new
        {
            Status = previousStatus.ToString()
        });
        using var newData = JsonSerializer.SerializeToDocument(new
        {
            Status = drone.Status.ToString(),
            MissionId = mission.Id
        });
        auditWriter.AddUserAction(unitOfWork, mission.TenantId, mission.FarmId,
            actorId, executionContext.CorrelationId, nameof(Drone), drone.Id,
            "MISSION_STATUS_CHANGE", oldData, newData, changedAt);
    }

    private static string GetMissionAction(MissionStatus status) => status switch
    {
        MissionStatus.InFlight => "START_FLIGHT",
        MissionStatus.FlightCompleted => "COMPLETE_FLIGHT",
        MissionStatus.FlightFailed => "FAIL_FLIGHT",
        MissionStatus.Cancelled => "CANCEL",
        _ => "CHANGE_STATUS"
    };

    private static string? NormalizeReason(string? reason) =>
        string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

    private static bool HasValidFailureIncident(TransitionMissionCommand request) =>
        request.IncidentOperationId is Guid operationId && operationId != Guid.Empty &&
        !string.IsNullOrWhiteSpace(request.Reason) && request.Reason.Length <= 1000 &&
        !request.Reason.Any(char.IsControl) &&
        request.IncidentType is "SIGNAL_LOSS" or "LOW_BATTERY" or
            "INTERRUPTION" or "FLIGHT_FAILURE" &&
        !string.IsNullOrWhiteSpace(request.IncidentOutcome) &&
        request.IncidentOutcome.Length <= 1000 &&
        request.RecoveryDecision is "ABORT" or "RESCHEDULE_REQUIRED" &&
        !string.IsNullOrWhiteSpace(request.EvidenceReference) &&
        request.EvidenceReference.Length <= 500;
}
