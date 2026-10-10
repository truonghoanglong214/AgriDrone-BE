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

namespace AgriDrone.Modules.Missions.Application.Features.Missions.PrepareMissionSet;

internal sealed class PrepareMissionSetCommandHandler(
    ISurveyOrderMissionPlanningQuery orderPlanningQuery,
    ISystemManagerAccessService managerAccessService,
    IDroneMissionRepository missionRepository,
    IDroneQueries droneQueries,
    IMissionsUnitOfWork unitOfWork,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    TimeProvider timeProvider)
    : IRequestHandler<PrepareMissionSetCommand, Result<PrepareMissionSetResult>>
{
    public async Task<Result<PrepareMissionSetResult>> Handle(
        PrepareMissionSetCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<PrepareMissionSetResult>(
                MissionError.CurrentUserRequired());
        }

        SurveyOrderMissionPlanningContext? order;
        try
        {
            order = await orderPlanningQuery.GetAsync(
                request.SurveyOrderId,
                cancellationToken);
        }
        catch (SurveyOrderMissionPlanningUnavailableException)
        {
            return Result.Failure<PrepareMissionSetResult>(
                PrepareMissionSetErrors.OrderContextUnavailable());
        }

        if (order is null)
        {
            return Result.Failure<PrepareMissionSetResult>(
                PrepareMissionSetErrors.OrderNotFound(request.SurveyOrderId));
        }

        if (order.SurveyOrderId != request.SurveyOrderId ||
            order.TenantId == Guid.Empty ||
            order.FarmId == Guid.Empty ||
            !Enum.IsDefined(order.SelectedService) ||
            (order.IsReadyToSchedule &&
             (order.AppointmentStartAt.Offset != TimeSpan.Zero ||
              order.AppointmentEndAt.Offset != TimeSpan.Zero ||
              order.AppointmentEndAt <= order.AppointmentStartAt)) ||
            order.ScopeZoneIds is null ||
            order.ScopeZoneIds.Any(zoneId => zoneId == Guid.Empty) ||
            order.ScopeZoneIds.Distinct().Count() !=
            order.ScopeZoneIds.Count ||
            order.FarmBoundaryVersionId is null ||
            order.FarmBoundaryVersionId == Guid.Empty ||
            (!order.RequiresBaselineMapping &&
             order.CurrentBaseMapVersionId is null))
        {
            return Result.Failure<PrepareMissionSetResult>(
                PrepareMissionSetErrors.InvalidOrderContext());
        }

        var access = await managerAccessService.ResolveFarmAccessAsync(
            order.FarmId,
            cancellationToken);
        if (!access.IsAllowed)
        {
            return Result.Failure<PrepareMissionSetResult>(
                PrepareMissionSetErrors.FarmAccessDenied());
        }

        if (access.TenantId != order.TenantId ||
            access.FarmId != order.FarmId)
        {
            return Result.Failure<PrepareMissionSetResult>(
                PrepareMissionSetErrors.CrossTenantOrder());
        }

        var existing = await missionRepository.GetBySurveyOrderIdAsync(
            order.SurveyOrderId,
            cancellationToken);
        var expectedPurposes = GetExpectedPurposes(order, existing);
        var servicePurpose = order.SelectedService switch
        {
            SurveyServiceType.PlantHealth => MissionPurpose.PlantHealth,
            SurveyServiceType.HarvestReadiness => MissionPurpose.HarvestReadiness,
            _ => throw new InvalidOperationException(
                $"Unsupported Survey Service '{order.SelectedService}'.")
        };

        if (!order.IsEligibleForPlanning)
        {
            return Result.Failure<PrepareMissionSetResult>(
                PrepareMissionSetErrors.OrderNotReady(order.ReadinessFailureCode));
        }

        var purposeContexts = new Dictionary<MissionPurpose, SurveyOrderMissionPlanningContext>
        {
            [order.RequiresBaselineMapping
                ? MissionPurpose.BaselineMapping
                : servicePurpose] = order
        };
        if (!order.RequiresBaselineMapping &&
            expectedPurposes.Contains(MissionPurpose.BaselineMapping))
        {
            purposeContexts[MissionPurpose.BaselineMapping] =
                order with { IsReadyToSchedule = false };
        }
        if (HasInvalidExistingSet(existing, expectedPurposes, order))
        {
            return Result.Failure<PrepareMissionSetResult>(
                PrepareMissionSetErrors.ExistingMissionSetRequiresRecovery());
        }

        if (HasCompleteSet(existing, expectedPurposes) &&
            existing.All(mission =>
                mission.Status != MissionStatus.Draft ||
                !purposeContexts[mission.Purpose!.Value].IsReadyToSchedule))
        {
            return Result.Success(MapResult(
                order.SurveyOrderId,
                order.FarmId,
                existing,
                true));
        }

        var scheduleValidation = ValidateSchedules(
            request, order, purposeContexts, existing);
        if (scheduleValidation is not null)
        {
            return Result.Failure<PrepareMissionSetResult>(scheduleValidation);
        }

        foreach (var purpose in expectedPurposes)
        {
            if (!purposeContexts[purpose].IsReadyToSchedule ||
                existing.Any(mission => mission.Purpose == purpose &&
                    mission.Status == MissionStatus.Scheduled))
            {
                continue;
            }

            var window = purpose == MissionPurpose.BaselineMapping
                ? request.BaselineWindow!
                : request.ServiceWindow!;
            var availableDrones = await droneQueries.GetAvailableAsync(
                window.StartAt,
                window.EndAt,
                cancellationToken);
            if (availableDrones.All(drone =>
                    drone.Id != request.DroneId ||
                    !DroneCapabilityPolicy.Supports(drone.Specifications, purpose)))
            {
                return Result.Failure<PrepareMissionSetResult>(
                    PrepareMissionSetErrors.DroneNotAvailable(request.DroneId));
            }
        }

        var now = timeProvider.GetUtcNow();
        var created = new List<DroneMission>();
        using var flightParameters = JsonDocument.Parse("{}");

        foreach (var purpose in expectedPurposes)
        {
            var draft = existing.SingleOrDefault(mission =>
                mission.Purpose == purpose && mission.Status == MissionStatus.Draft);
            if (draft is not null)
            {
                if (purposeContexts[purpose].IsReadyToSchedule)
                {
                    var window = purpose == MissionPurpose.BaselineMapping
                        ? request.BaselineWindow!
                        : request.ServiceWindow!;
                    draft.Schedule(window.StartAt, window.EndAt, now);
                    AddScheduleAudit(draft, actorId, now);
                }
                continue;
            }
            if (existing.Any(mission => mission.Purpose == purpose))
                continue;

            var sourceMapVersionId = purpose == MissionPurpose.BaselineMapping
                ? null
                : order.CurrentBaseMapVersionId;

            var mission = DroneMission.CreateOrderBound(
                order.SurveyOrderId,
                order.TenantId,
                order.FarmId,
                order.ScopeZoneIds,
                order.FarmBoundaryVersionId!.Value,
                request.DroneId,
                actorId,
                BuildMissionCode(order.SurveyOrderId, purpose),
                purpose,
                request.OperationId,
                sourceMapVersionId,
                false,
                flightParameters,
                actorId,
                now);
            if (purposeContexts[purpose].IsReadyToSchedule)
            {
                var window = purpose == MissionPurpose.BaselineMapping
                    ? request.BaselineWindow!
                    : request.ServiceWindow!;
                mission.Schedule(window.StartAt, window.EndAt, now);
            }
            missionRepository.Add(mission);
            AddAudit(mission, actorId, now);
            created.Add(mission);
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (MissionSetConflictException)
        {
            var concurrentlyPrepared =
                await missionRepository.GetBySurveyOrderIdAsync(
                    order.SurveyOrderId,
                    cancellationToken);
            if (!HasInvalidExistingSet(concurrentlyPrepared, expectedPurposes, order) &&
                HasCompleteSet(concurrentlyPrepared, expectedPurposes))
            {
                return Result.Success(MapResult(
                    order.SurveyOrderId,
                    order.FarmId,
                    concurrentlyPrepared,
                    true));
            }

            return Result.Failure<PrepareMissionSetResult>(
                PrepareMissionSetErrors.ConcurrentPreparation());
        }
        catch (MissionScheduleConflictException)
        {
            return Result.Failure<PrepareMissionSetResult>(
                PrepareMissionSetErrors.DroneNotAvailable(request.DroneId));
        }
        catch (MissionConcurrencyException)
        {
            return Result.Failure<PrepareMissionSetResult>(
                PrepareMissionSetErrors.ConcurrentPreparation());
        }

        return Result.Success(MapResult(
            order.SurveyOrderId,
            order.FarmId,
            existing.Concat(created),
            false));
    }

    private void AddAudit(
        DroneMission mission,
        Guid actorId,
        DateTimeOffset createdAt)
    {
        using var data = JsonSerializer.SerializeToDocument(new
        {
            mission.SurveyOrderId,
            Purpose = mission.Purpose!.Value.ToString(),
            mission.DroneId,
            mission.ScopeZoneIds,
            mission.FarmBoundaryVersionId,
            mission.ScheduledAt,
            mission.ScheduledEndAt,
            mission.RequiresBaselineCompletion,
            mission.SourceMapVersionId,
            mission.PreparationOperationId
        });
        auditWriter.AddUserAction(
            unitOfWork,
            mission.TenantId,
            mission.FarmId,
            actorId,
            executionContext.CorrelationId,
            nameof(DroneMission),
            mission.Id,
            "PREPARE_FROM_SURVEY_ORDER",
            null,
            data,
            createdAt);
    }

    private void AddScheduleAudit(
        DroneMission mission, Guid actorId, DateTimeOffset changedAt)
    {
        using var oldData = JsonSerializer.SerializeToDocument(new
        {
            Status = MissionStatus.Draft.ToString(),
            ScheduledAt = (DateTimeOffset?)null,
            ScheduledEndAt = (DateTimeOffset?)null
        });
        using var newData = JsonSerializer.SerializeToDocument(new
        {
            Status = mission.Status.ToString(),
            mission.ScheduledAt,
            mission.ScheduledEndAt
        });
        auditWriter.AddUserAction(
            unitOfWork, mission.TenantId, mission.FarmId, actorId,
            executionContext.CorrelationId, nameof(DroneMission), mission.Id,
            "SCHEDULE_PREPARED_MISSION", oldData, newData, changedAt);
    }

    private static IReadOnlyList<MissionPurpose> GetExpectedPurposes(
        SurveyOrderMissionPlanningContext order,
        IReadOnlyCollection<DroneMission> existing)
    {
        var servicePurpose = order.SelectedService switch
        {
            SurveyServiceType.PlantHealth => MissionPurpose.PlantHealth,
            SurveyServiceType.HarvestReadiness => MissionPurpose.HarvestReadiness,
            _ => throw new InvalidOperationException(
                $"Unsupported Survey Service '{order.SelectedService}'.")
        };

        if (order.RequiresBaselineMapping)
            return [MissionPurpose.BaselineMapping];

        return existing.Any(mission => mission.Purpose == MissionPurpose.BaselineMapping)
            ? [MissionPurpose.BaselineMapping, servicePurpose]
            : [servicePurpose];
    }

    private static AppError? ValidateSchedules(
        PrepareMissionSetCommand request,
        SurveyOrderMissionPlanningContext order,
        IReadOnlyDictionary<MissionPurpose, SurveyOrderMissionPlanningContext> purposeContexts,
        IReadOnlyCollection<DroneMission> existing)
    {
        if (order.RequiresBaselineMapping && request.ServiceWindow is not null)
            return PrepareMissionSetErrors.ServiceWindowNotAllowed();
        if (!order.RequiresBaselineMapping && request.BaselineWindow is not null)
            return PrepareMissionSetErrors.BaselineWindowNotAllowed();

        foreach (var (purpose, context) in purposeContexts)
        {
            if (!context.IsReadyToSchedule)
                continue;
            var window = purpose == MissionPurpose.BaselineMapping
                ? request.BaselineWindow
                : request.ServiceWindow;
            if (window is null)
                return purpose == MissionPurpose.BaselineMapping
                    ? PrepareMissionSetErrors.BaselineWindowRequired()
                    : PrepareMissionSetErrors.ServiceWindowRequired();
            if (window.StartAt < context.AppointmentStartAt ||
                window.EndAt > context.AppointmentEndAt)
                return PrepareMissionSetErrors.ScheduleOutsideAppointment();
        }

        var baselinePublishedAt = existing.FirstOrDefault(mission =>
            mission.Purpose == MissionPurpose.BaselineMapping)?.MapPublishedAt;
        if (request.ServiceWindow is { } serviceWindow &&
            baselinePublishedAt.HasValue &&
            serviceWindow.StartAt < baselinePublishedAt.Value)
            return PrepareMissionSetErrors.BaselineMustPrecedeService();

        return null;
    }

    private static bool HasCompleteSet(
        IReadOnlyCollection<DroneMission> existing,
        IReadOnlyCollection<MissionPurpose> expectedPurposes) =>
        existing.Count == expectedPurposes.Count &&
        expectedPurposes.All(purpose =>
            existing.Count(mission => mission.Purpose == purpose) == 1);

    private static bool HasInvalidExistingSet(
        IReadOnlyCollection<DroneMission> existing,
        IReadOnlyCollection<MissionPurpose> expectedPurposes,
        SurveyOrderMissionPlanningContext order) =>
        existing.Any(mission =>
            mission.TenantId != order.TenantId ||
            mission.FarmId != order.FarmId ||
            mission.FarmBoundaryVersionId != order.FarmBoundaryVersionId ||
            !mission.ScopeZoneIds.Order().SequenceEqual(order.ScopeZoneIds.Order()) ||
            mission.Purpose is not { } purpose ||
            !expectedPurposes.Contains(purpose) ||
            mission.Status is MissionStatus.Cancelled or MissionStatus.FlightFailed ||
            (mission.Status == MissionStatus.Draft
                ? mission.ScheduledAt.HasValue || mission.ScheduledEndAt.HasValue
                : mission.ScheduledAt is null || mission.ScheduledEndAt is null) ||
            (purpose == MissionPurpose.BaselineMapping &&
             (mission.SourceMapVersionId is not null ||
              mission.RequiresBaselineCompletion)) ||
            (purpose != MissionPurpose.BaselineMapping &&
             (mission.RequiresBaselineCompletion
                 ? !order.RequiresBaselineMapping ||
                   order.CurrentBaseMapVersionId is not null ||
                   mission.SourceMapVersionId is not null
                 : mission.SourceMapVersionId != order.CurrentBaseMapVersionId))) ||
        expectedPurposes.Any(purpose =>
            existing.Count(mission => mission.Purpose == purpose) > 1);

    private static PrepareMissionSetResult MapResult(
        Guid surveyOrderId,
        Guid farmId,
        IEnumerable<DroneMission> missions,
        bool reusedExistingSet)
    {
        var materialized = missions
            .OrderBy(mission => mission.Purpose)
            .Select(mission => new PreparedMissionResult(
                mission.Id,
                mission.Purpose!.Value,
                mission.Status,
                mission.ScheduledAt,
                mission.ScheduledEndAt,
                mission.Version))
            .ToArray();
        return new PrepareMissionSetResult(
            surveyOrderId,
            farmId,
            reusedExistingSet,
            materialized);
    }

    private static string BuildMissionCode(
        Guid surveyOrderId,
        MissionPurpose purpose)
    {
        var suffix = purpose switch
        {
            MissionPurpose.BaselineMapping => "BASE",
            MissionPurpose.PlantHealth => "HEALTH",
            MissionPurpose.HarvestReadiness => "READY",
            _ => throw new ArgumentOutOfRangeException(nameof(purpose))
        };
        return $"ORD-{surveyOrderId:N}-{suffix}"[..Math.Min(
            50,
            $"ORD-{surveyOrderId:N}-{suffix}".Length)];
    }
}
