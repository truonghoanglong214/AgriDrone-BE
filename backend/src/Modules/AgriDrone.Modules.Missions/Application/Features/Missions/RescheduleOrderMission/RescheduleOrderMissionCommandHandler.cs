using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions;
using AgriDrone.Modules.Missions.Application.Abstractions.MissionPlanning;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Application.Features.Missions.TransitionMission;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Missions.Application.Features.Missions.RescheduleOrderMission;

internal sealed class RescheduleOrderMissionCommandHandler(
    IDroneMissionRepository missions,
    ISurveyOrderMissionPlanningQuery orders,
    IDroneQueries drones,
    IPreflightChecklistRepository checklists,
    IMissionFieldNoteRepository fieldNotes,
    ISystemManagerAccessService managerAccess,
    IMissionsUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider clock)
    : IRequestHandler<RescheduleOrderMissionCommand, Result<MissionResponse>>
{
    public async Task<Result<MissionResponse>> Handle(
        RescheduleOrderMissionCommand request, CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
            return Result.Failure<MissionResponse>(MissionError.CurrentUserRequired());

        var access = await managerAccess.ResolveFarmAccessAsync(request.FarmId,
            cancellationToken);
        if (!access.IsAllowed || access.TenantId is not Guid tenantId ||
            access.FarmId != request.FarmId)
            return Result.Failure<MissionResponse>(MissionOperationError.FarmAccessDenied());

        var mission = await missions.GetByIdAsync(request.MissionId, tenantId,
            request.FarmId, cancellationToken);
        if (mission is null)
            return Result.Failure<MissionResponse>(MissionError.NotFound(request.MissionId));
        if (mission.SurveyOrderId is not Guid orderId || mission.Purpose is not MissionPurpose purpose)
            return Result.Failure<MissionResponse>(MissionOperationError.MissionNotOrderBound());
        var recovering = mission.Status == MissionStatus.FlightFailed;
        if (mission.Status is not (MissionStatus.Scheduled or MissionStatus.FlightFailed))
            return Result.Failure<MissionResponse>(MissionError.InvalidTransition(
                mission.Status, MissionStatus.Scheduled));
        if (mission.Version != request.ExpectedVersion)
            return Result.Failure<MissionResponse>(MissionError.VersionConflict(
                request.ExpectedVersion, mission.Version));
        if (!recovering && request.ReplacementDroneId is not null)
            return Result.Failure<MissionResponse>(MissionOperationError.ReplacementDroneOnlyOnRecovery());
        if (!recovering && mission.ScheduledAt == request.StartAt &&
            mission.ScheduledEndAt == request.EndAt)
            return Result.Success(MissionResponseMapper.Map(mission));

        SurveyOrderMissionPlanningContext? order;
        try
        {
            order = await orders.GetForPurposeAsync(orderId, purpose, cancellationToken);
        }
        catch (SurveyOrderMissionPlanningUnavailableException)
        {
            return Result.Failure<MissionResponse>(MissionOperationError.OrderContextUnavailable());
        }
        if (order is null)
            return Result.Failure<MissionResponse>(MissionOperationError.OrderNotFound(orderId));
        if (order.SurveyOrderId != orderId || order.TenantId != tenantId ||
            order.FarmId != request.FarmId ||
            purpose == MissionPurpose.PlantHealth &&
            order.SelectedService != SurveyServiceType.PlantHealth ||
            purpose == MissionPurpose.HarvestReadiness &&
            order.SelectedService != SurveyServiceType.HarvestReadiness)
            return Result.Failure<MissionResponse>(MissionOperationError.InvalidOrderContext());
        if (order.FarmBoundaryVersionId != mission.FarmBoundaryVersionId ||
            order.ScopeZoneIds is null ||
            !order.ScopeZoneIds.Order().SequenceEqual(mission.ScopeZoneIds.Order()) ||
            access.ManagerProfileId is null ||
            order.PrimarySystemManagerId != access.ManagerProfileId)
            return Result.Failure<MissionResponse>(MissionOperationError.InvalidOrderContext());
        if (!order.IsReadyForOperations || !order.IsReadyToSchedule)
            return Result.Failure<MissionResponse>(MissionOperationError.OrderNotReady(
                order.ReadinessFailureCode));
        if (request.StartAt < order.AppointmentStartAt ||
            request.EndAt > order.AppointmentEndAt)
            return Result.Failure<MissionResponse>(MissionOperationError.AppointmentWindowClosed());
        if (purpose != MissionPurpose.BaselineMapping &&
            mission.SourceMapVersionId != order.CurrentBaseMapVersionId)
            return Result.Failure<MissionResponse>(MissionOperationError.BaselineNotCompleted());

        if (recovering)
        {
            if (mission.EndedAt is null ||
                request.StartAt <= mission.EndedAt ||
                request.StartAt <= clock.GetUtcNow())
                return Result.Failure<MissionResponse>(
                    MissionOperationError.RecoveryWindowRequired());
            var notes = await fieldNotes.ListAsync(tenantId, request.FarmId,
                mission.Id, cancellationToken);
            if (!notes.Any(note => note.IncidentType is not null &&
                note.RecoveryDecision == "RESCHEDULE_REQUIRED" &&
                note.ReceivedAt == mission.EndedAt))
                return Result.Failure<MissionResponse>(
                    MissionOperationError.RecoveryDecisionRequired());
        }

        var selectedDroneId = recovering
            ? request.ReplacementDroneId ?? mission.DroneId
            : mission.DroneId;
        if (selectedDroneId == Guid.Empty)
            return Result.Failure<MissionResponse>(MissionError.DroneNotAvailable(selectedDroneId));
        var available = await drones.GetAvailableExcludingMissionAsync(
            request.StartAt, request.EndAt, mission.Id, cancellationToken);
        if (available.All(drone => drone.Id != selectedDroneId ||
            !DroneCapabilityPolicy.Supports(drone.Specifications, purpose)))
            return Result.Failure<MissionResponse>(MissionError.DroneNotAvailable(selectedDroneId));

        var oldStart = mission.ScheduledAt;
        var oldEnd = mission.ScheduledEndAt;
        var oldFlightStart = mission.StartedAt;
        var oldFlightEnd = mission.EndedAt;
        var oldStatus = mission.Status;
        var oldDroneId = mission.DroneId;
        var oldPreflight = mission.PreflightOperationId;
        var now = clock.GetUtcNow();
        if (oldPreflight is Guid operationId)
        {
            var snapshot = await checklists.GetByOperationIdAsync(
                mission.Id, operationId, cancellationToken);
            snapshot?.Supersede();
        }
        if (recovering)
            mission.RecoverFailedFlight(selectedDroneId,
                request.StartAt, request.EndAt, now);
        else
            mission.Reschedule(request.StartAt, request.EndAt, now);
        using var oldData = JsonSerializer.SerializeToDocument(new
        {
            Status = oldStatus.ToString(),
            DroneId = oldDroneId,
            ScheduledAt = oldStart, ScheduledEndAt = oldEnd,
            StartedAt = oldFlightStart, EndedAt = oldFlightEnd,
            PreflightOperationId = oldPreflight
        });
        using var newData = JsonSerializer.SerializeToDocument(new
        {
            Status = mission.Status.ToString(),
            mission.DroneId,
            mission.ScheduledAt, mission.ScheduledEndAt,
            mission.StartedAt, mission.EndedAt,
            mission.PreflightOperationId
        });
        auditWriter.AddUserAction(unitOfWork, mission.TenantId, mission.FarmId,
            actorId, executionContext.CorrelationId, nameof(DroneMission), mission.Id,
            recovering ? "RECOVER_FAILED_FLIGHT" : "RESCHEDULE",
            oldData, newData, now);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (MissionConcurrencyException)
        {
            return Result.Failure<MissionResponse>(MissionError.ConcurrentUpdate());
        }
        catch (MissionScheduleConflictException)
        {
            return Result.Failure<MissionResponse>(MissionError.DroneNotAvailable(selectedDroneId));
        }
        catch (PreflightChecklistConflictException)
        {
            return Result.Failure<MissionResponse>(MissionError.ConcurrentUpdate());
        }
        return Result.Success(MissionResponseMapper.Map(mission));
    }
}
