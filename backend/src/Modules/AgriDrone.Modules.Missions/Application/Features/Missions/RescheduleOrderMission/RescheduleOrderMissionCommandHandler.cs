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
        if (mission.Status != MissionStatus.Scheduled)
            return Result.Failure<MissionResponse>(MissionError.InvalidTransition(
                mission.Status, MissionStatus.Scheduled));
        if (mission.ScheduledAt == request.StartAt &&
            mission.ScheduledEndAt == request.EndAt)
            return Result.Success(MissionResponseMapper.Map(mission));
        if (mission.Version != request.ExpectedVersion)
            return Result.Failure<MissionResponse>(MissionError.VersionConflict(
                request.ExpectedVersion, mission.Version));

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
        if (!order.IsReadyForOperations)
            return Result.Failure<MissionResponse>(MissionOperationError.OrderNotReady(
                order.ReadinessFailureCode));
        if (request.StartAt < order.AppointmentStartAt ||
            request.EndAt > order.AppointmentEndAt)
            return Result.Failure<MissionResponse>(MissionOperationError.AppointmentWindowClosed());
        if (purpose != MissionPurpose.BaselineMapping &&
            mission.SourceMapVersionId != order.CurrentBaseMapVersionId)
            return Result.Failure<MissionResponse>(MissionOperationError.BaselineNotCompleted());

        var available = await drones.GetAvailableExcludingMissionAsync(
            request.StartAt, request.EndAt, mission.Id, cancellationToken);
        if (available.All(drone => drone.Id != mission.DroneId ||
            !DroneCapabilityPolicy.Supports(drone.Specifications, purpose)))
            return Result.Failure<MissionResponse>(MissionError.DroneNotAvailable(mission.DroneId));

        var oldStart = mission.ScheduledAt;
        var oldEnd = mission.ScheduledEndAt;
        var oldPreflight = mission.PreflightOperationId;
        var now = clock.GetUtcNow();
        if (oldPreflight is Guid operationId)
        {
            var snapshot = await checklists.GetByOperationIdAsync(
                mission.Id, operationId, cancellationToken);
            snapshot?.Supersede();
        }
        mission.Reschedule(request.StartAt, request.EndAt, now);
        using var oldData = JsonSerializer.SerializeToDocument(new
        {
            ScheduledAt = oldStart, ScheduledEndAt = oldEnd,
            PreflightOperationId = oldPreflight
        });
        using var newData = JsonSerializer.SerializeToDocument(new
        {
            mission.ScheduledAt, mission.ScheduledEndAt,
            mission.PreflightOperationId
        });
        auditWriter.AddUserAction(unitOfWork, mission.TenantId, mission.FarmId,
            actorId, executionContext.CorrelationId, nameof(DroneMission), mission.Id,
            "RESCHEDULE", oldData, newData, now);

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
            return Result.Failure<MissionResponse>(MissionError.DroneNotAvailable(mission.DroneId));
        }
        catch (PreflightChecklistConflictException)
        {
            return Result.Failure<MissionResponse>(MissionError.ConcurrentUpdate());
        }
        return Result.Success(MissionResponseMapper.Map(mission));
    }
}
