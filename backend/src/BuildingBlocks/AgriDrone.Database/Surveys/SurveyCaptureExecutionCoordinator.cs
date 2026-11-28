using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Domain.Maps.Staging;
using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Domain.Processing;
using AgriDrone.Modules.Missions.Domain.Telemetry;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Messaging.Outbox;
using AgriDrone.SharedInfrastructure.Messaging.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace AgriDrone.Database.Surveys;

internal sealed class SurveyCaptureExecutionCoordinator(
    AgriDroneSchemaDbContext db,
    TimeProvider clock,
    OutboxMessageFactory outboxFactory,
    ISurveyCaptureExecutionProbe? probe = null,
    IObjectStorage? objectStorage = null) : ISurveyCaptureExecutionCoordinator
{
    private readonly ISurveyCaptureExecutionProbe _probe = probe ?? new NoopSurveyCaptureExecutionProbe();
    private readonly IObjectStorage? _objectStorage = objectStorage;
    public Task<CaptureDroneSelection?> GetDroneSelectionAsync(
        Guid orderId, Guid actorId, DateTimeOffset? startAt, DateTimeOffset? endAt,
        CancellationToken cancellationToken = default) => ExecuteAsync<CaptureDroneSelection?>(
        async token =>
        {
            var order = await db.Set<SurveyOrder>().AsNoTracking().SingleOrDefaultAsync(
                value => value.Id == orderId && value.Scope == ContractScope.V3, token);
            if (order is null || !await IsAssignedManagerAsync(order.TenantId, order.FarmId, actorId, token))
                return null;

            if (order.DepositObligationId is not { } obligationId ||
                (await db.Set<SurveyDepositObligation>().AsNoTracking().SingleOrDefaultAsync(
                    value => value.Id == obligationId && value.SurveyOrderId == orderId, token))?.IsSatisfied != true)
                return null;

            var isStagedCapture = await db.Set<CaptureAttempt>().AnyAsync(
                value => value.OrderId == orderId && value.MappingSourceAttemptId != null, token);
            var canScheduleDisease = order.Status == SurveyOrderStatus.Processing && order.RequiresBaselineMapping &&
                order.VerifiedPoleCountSnapshotId.HasValue && !order.CurrentApprovedResultRevisionId.HasValue;
            if (order.Status == SurveyOrderStatus.ReadyForCapture && startAt is null && endAt is null)
            {
                var appointment = await db.Set<SurveyAppointment>().AsNoTracking()
                    .Where(value => value.SurveyOrderId == orderId &&
                        (value.Purpose == SurveyAppointmentPurpose.Capture || value.Purpose == SurveyAppointmentPurpose.DiseaseCapture) &&
                        value.Status == SurveyAppointmentStatus.Confirmed)
                    .OrderByDescending(value => value.Purpose == SurveyAppointmentPurpose.DiseaseCapture)
                    .FirstOrDefaultAsync(token);
                startAt = appointment?.ProposedStartAt;
                endAt = appointment?.ProposedEndAt;
            }
            else if (!(order.Status == SurveyOrderStatus.AwaitingReflightDecision || canScheduleDisease ||
                       (isStagedCapture && order.Status == SurveyOrderStatus.ReadyForCapture)) ||
                     startAt is not { } schedulingStart || schedulingStart <= clock.GetUtcNow())
            {
                return null;
            }

            if (startAt is not { } start || endAt is not { } end || end <= start)
                return null;

            var commercial = await db.Set<SurveyOrderCommercialSnapshot>().AsNoTracking()
                .SingleOrDefaultAsync(value => value.SurveyOrderId == orderId, token);
            var diseaseOnly = canScheduleDisease || isStagedCapture;
            if (diseaseOnly && (commercial?.ServiceCode != "COMBO" || !commercial.ScreensDisease))
                return null;
            return commercial is null ? null : new CaptureDroneSelection(
                order.FarmId, start.ToUniversalTime(), end.ToUniversalTime(), commercial.MapsPoles && !diseaseOnly, commercial.ScreensDisease);
        },
        _ => null,
        cancellationToken);

    public Task<PrepareCaptureAttemptResult> PrepareAttemptAsync(
        PrepareCaptureAttemptRequest request,
        CancellationToken cancellationToken = default) => ExecuteAsync(
        async token =>
        {
            await LockOrderAsync(request.OrderId, token);
            var now = clock.GetUtcNow();
            var order = await db.Set<SurveyOrder>()
                .SingleOrDefaultAsync(value => value.Id == request.OrderId, token);
            if (order is null)
            {
                return PrepareFailure("ORDER_NOT_FOUND", "Survey Order was not found.");
            }
            if (!await IsAssignedManagerAsync(
                    order.TenantId,
                    order.FarmId,
                    request.ActorId,
                    token))
            {
                return PrepareFailure("MANAGER_FORBIDDEN", "The actor is not the active assigned Manager.");
            }

            var replay = await db.Set<DroneMission>()
                .AsNoTracking()
                .Where(value =>
                    value.SurveyOrderId == request.OrderId &&
                    value.PreparationOperationId == request.OperationId &&
                    value.CaptureAttemptId != null)
                .Select(value => new
                {
                    value.Id,
                    AttemptId = value.CaptureAttemptId!.Value
                })
                .SingleOrDefaultAsync(token);
            if (replay is not null)
            {
                var compatible = await IsOperationReplayCompatibleAsync(
                    replay.AttemptId,
                    request.OperationId,
                    request.PayloadFingerprint,
                    token);
                if (!compatible)
                {
                    return PrepareFailure("OPERATION_CONFLICT", "The operation identifier was already used with another payload.");
                }
                var replayAttempt = await db.Set<CaptureAttempt>()
                    .AsNoTracking()
                    .SingleAsync(value => value.Id == replay.AttemptId, token);
                return new PrepareCaptureAttemptResult(
                    true,
                    replayAttempt.Id,
                    replayAttempt.AttemptIndex,
                    replay.Id,
                    replayAttempt.Status.ToString(),
                    replayAttempt.Version);
            }

            if (order.Scope != ContractScope.V3 ||
                order.Status != SurveyOrderStatus.ReadyForCapture ||
                order.Version != request.ExpectedOrderVersion)
            {
                return PrepareFailure(
                    "ORDER_NOT_READY",
                    "The V3 Order is not at the expected ReadyForCapture version.");
            }
            if (!order.RequiresBaselineMapping &&
                (!order.BaselineMapVersionId.HasValue ||
                 !order.FarmBaseMapVersionId.HasValue))
            {
                return PrepareFailure(
                    "FOLLOW_UP_BASELINE_NOT_PINNED",
                    "A Follow-up Order requires its released baseline Order and exact baseline version.");
            }
            if (request.DroneId is not Guid droneId || droneId == Guid.Empty)
            {
                return PrepareFailure("DRONE_REQUIRED", "A drone is required.");
            }
            var appointment = await db.Set<SurveyAppointment>()
                .SingleOrDefaultAsync(value =>
                    value.SurveyOrderId == order.Id &&
                    value.Purpose == SurveyAppointmentPurpose.Capture &&
                    value.Status == SurveyAppointmentStatus.Confirmed,
                    token);
            var obligation = order.DepositObligationId.HasValue
                ? await db.Set<SurveyDepositObligation>()
                    .SingleOrDefaultAsync(value =>
                        value.Id == order.DepositObligationId.Value &&
                        value.SurveyOrderId == order.Id,
                        token)
                : null;
            if (appointment is null || obligation?.IsSatisfied != true)
            {
                return PrepareFailure(
                    "COMMERCIAL_NOT_READY",
                    "A confirmed Capture appointment and satisfied pinned deposit are required.");
            }
            if (await db.Set<CaptureAttempt>().AnyAsync(value =>
                    value.OrderId == order.Id &&
                    value.Status != CaptureAttemptStatus.Accepted &&
                    value.Status != CaptureAttemptStatus.ReflightRequired &&
                    value.Status != CaptureAttemptStatus.FlightFailed &&
                    value.Status != CaptureAttemptStatus.Cancelled,
                    token))
            {
                return PrepareFailure("ACTIVE_ATTEMPT_EXISTS", "An active Capture Attempt already exists.");
            }

            await LockDroneAsync(droneId, token);
            var drone = await db.Set<Drone>()
                .SingleOrDefaultAsync(value => value.Id == droneId, token);
            if (drone is null ||
                !drone.IsOperationalFor(
                    appointment.ProposedStartAt,
                    appointment.ProposedEndAt))
            {
                return PrepareFailure("DRONE_NOT_READY", "The selected drone is not operational for the appointment window.");
            }

            var commercial = await db.Set<SurveyOrderCommercialSnapshot>()
                .AsNoTracking()
                .SingleOrDefaultAsync(value => value.SurveyOrderId == order.Id, token);
            if (commercial is null ||
                (commercial.MapsPoles && !drone.SupportsPurpose(MissionPurpose.BaselineMapping)) ||
                (commercial.ScreensDisease && !drone.SupportsPurpose(MissionPurpose.PlantHealth)))
            {
                return PrepareFailure("DRONE_CAPABILITY_MISMATCH", "The drone does not satisfy the pinned service capabilities.");
            }

            var attemptId = Guid.CreateVersion7();
            using var flightParameters = JsonSerializer.SerializeToDocument(new
            {
                contract = "SURVEY_CAPTURE_V3",
                orderId = order.Id,
                captureAttemptId = attemptId,
                commercial.MapsPoles,
                commercial.ScreensDisease,
                commercial.PolicyFingerprint
            });
            var mission = DroneMission.CreateOrderBound(
                order.Id,
                order.TenantId,
                order.FarmId,
                [],
                order.FarmBoundaryVersionId,
                drone.Id,
                request.ActorId,
                $"V3CAP-{order.Id:N}",
                MissionPurpose.Capture,
                request.OperationId,
                order.FarmBaseMapVersionId,
                requiresBaselineCompletion: false,
                flightParameters,
                request.ActorId,
                now);
            var attempt = CaptureAttempt.CreateInitial(
                attemptId,
                order.Id,
                order.TenantId,
                order.FarmId,
                mission.Id,
                request.ActorId,
                "V3_CAPTURE_COORDINATOR",
                request.Reason ?? "Initial capture preparation",
                request.CorrelationId,
                request.OperationId,
                request.PayloadFingerprint,
                now);
            mission.BindCaptureAttempt(attempt.Id);
            mission.Schedule(
                appointment.ProposedStartAt,
                appointment.ProposedEndAt,
                now);
            db.Set<CaptureAttempt>().Add(attempt);
            db.Set<DroneMission>().Add(mission);
            AddCaptureEvidence(order, request.ActorId, request.CorrelationId,
                "ATTEMPT_PREPARED", now, attempt.Id, mission.Id);
            await db.SaveChangesAsync(token);

            return new PrepareCaptureAttemptResult(
                true,
                attempt.Id,
                attempt.AttemptIndex,
                mission.Id,
                attempt.Status.ToString(),
                attempt.Version);
        },
        PrepareException,
        cancellationToken);

    public Task<PrepareCaptureAttemptResult> ScheduleDiseaseCaptureAsync(
        ScheduleDiseaseCaptureRequest request,
        CancellationToken cancellationToken = default) => ExecuteAsync(
        async token =>
        {
            await LockOrderAsync(request.OrderId, token);
            var order = await db.Set<SurveyOrder>().SingleOrDefaultAsync(value => value.Id == request.OrderId, token);
            if (order is null)
                return PrepareFailure("ORDER_NOT_FOUND", "Survey Order was not found.");
            if (!await IsAssignedManagerAsync(order.TenantId, order.FarmId, request.ActorId, token))
                return PrepareFailure("MANAGER_FORBIDDEN", "The actor is not the active assigned Manager.");

            var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
                $"V3_DISEASE_CAPTURE|{request.OrderId:D}|{request.ActorId:D}|{request.DroneId:D}|{request.StartAt:O}|{request.EndAt:O}|{request.ExpectedOrderVersion}")));
            var replay = await db.Set<DroneMission>().AsNoTracking()
                .SingleOrDefaultAsync(value => value.SurveyOrderId == order.Id &&
                    value.PreparationOperationId == request.OperationId && value.CaptureAttemptId != null, token);
            if (replay?.CaptureAttemptId is Guid replayAttemptId)
            {
                var existing = await db.Set<CaptureAttempt>().AsNoTracking().SingleAsync(value => value.Id == replayAttemptId, token);
                if (!existing.MappingSourceAttemptId.HasValue ||
                    !await IsOperationReplayCompatibleAsync(existing.Id, request.OperationId, fingerprint, token))
                    return PrepareFailure("OPERATION_CONFLICT", "The operation identifier was already used with another payload.");

                return new PrepareCaptureAttemptResult(true, existing.Id, existing.AttemptIndex, replay.Id,
                    existing.Status.ToString(), existing.Version);
            }

            var now = clock.GetUtcNow();
            if (request.DroneId == Guid.Empty || request.OperationId == Guid.Empty ||
                request.StartAt <= now || request.EndAt <= request.StartAt)
                return PrepareFailure("APPOINTMENT_WINDOW_INVALID", "A drone, operation identifier and future disease capture window are required.");
            if (order.Scope != ContractScope.V3 || order.Version != request.ExpectedOrderVersion ||
                order.Status != SurveyOrderStatus.Processing || !order.RequiresBaselineMapping ||
                order.VerifiedPoleCountSnapshotId is not Guid snapshotId ||
                order.CurrentApprovedResultRevisionId.HasValue || order.CurrentInvoiceRevisionId.HasValue)
                return PrepareFailure("ORDER_NOT_READY", "First COMBO must have a verified mapping result and an open result at the expected Order version.");

            var commercial = await db.Set<SurveyOrderCommercialSnapshot>().AsNoTracking()
                .SingleOrDefaultAsync(value => value.SurveyOrderId == order.Id, token);
            if (commercial?.ServiceCode != "COMBO" || !commercial.ScreensDisease || !commercial.MapsPoles)
                return PrepareFailure("DISEASE_CAPTURE_NOT_SUPPORTED", "Only First COMBO supports a separate disease capture.");
            if (order.DepositObligationId is not Guid obligationId ||
                (await db.Set<SurveyDepositObligation>().SingleOrDefaultAsync(value => value.Id == obligationId &&
                    value.SurveyOrderId == order.Id, token))?.IsSatisfied != true)
                return PrepareFailure("DEPOSIT_NOT_SATISFIED", "Disease capture requires the original satisfied deposit.");

            var snapshot = await db.Set<SurveyOrderPoleCountSnapshot>().AsNoTracking()
                .SingleOrDefaultAsync(value => value.Id == snapshotId && value.SurveyOrderId == order.Id &&
                    value.TenantId == order.TenantId && value.FarmId == order.FarmId, token);
            if (snapshot is null || snapshot.FarmBaseMapVersionId != order.FarmBaseMapVersionId ||
                snapshot.FarmBoundaryVersionId != order.FarmBoundaryVersionId ||
                !await db.Set<FarmBaseMapVersion>().AnyAsync(value =>
                    value.Id == snapshot.FarmBaseMapVersionId && value.SourceSurveyOrderId == order.Id &&
                    value.TenantId == order.TenantId && value.FarmId == order.FarmId &&
                    value.SourceMissionId == snapshot.MissionId && value.FarmBoundaryVersionId == snapshot.FarmBoundaryVersionId &&
                    value.IsBaseline && value.Status == FarmBaseMapStatus.InternalApproved, token) ||
                !await db.Set<MappingCandidateBatch>().AnyAsync(value =>
                    value.Id == snapshot.MappingCandidateBatchId && value.SurveyOrderId == order.Id &&
                    value.TenantId == order.TenantId && value.FarmId == order.FarmId &&
                    value.MissionId == snapshot.MissionId && value.CaptureAttemptId == snapshot.CaptureAttemptId &&
                    value.FarmBaseMapVersionId == snapshot.FarmBaseMapVersionId &&
                    value.Status == MappingCandidateBatchStatus.Applied, token))
                return PrepareFailure("DISEASE_CAPTURE_MAP_REQUIRED", "The verified count, applied candidates and internally approved map must belong to the first capture.");

            var mappingAttempt = await db.Set<CaptureAttempt>().Include(value => value.History)
                .SingleOrDefaultAsync(value => value.Id == snapshot.CaptureAttemptId && value.OrderId == order.Id &&
                    value.TenantId == order.TenantId && value.FarmId == order.FarmId, token);
            var mappingMission = await db.Set<DroneMission>().AsNoTracking().SingleOrDefaultAsync(value =>
                value.Id == snapshot.MissionId && value.SurveyOrderId == order.Id && value.TenantId == order.TenantId &&
                value.FarmId == order.FarmId && value.CaptureAttemptId == snapshot.CaptureAttemptId &&
                value.Purpose == MissionPurpose.Capture, token);
            if (mappingAttempt is null || mappingAttempt.Status != CaptureAttemptStatus.Processing ||
                mappingAttempt.MappingSourceAttemptId.HasValue || string.IsNullOrWhiteSpace(mappingAttempt.ProcessingJobReference) ||
                mappingMission is null)
                return PrepareFailure("DISEASE_CAPTURE_SOURCE_INVALID", "The map must belong to the active first capture mission.");

            if (await db.Set<AiProcessingJob>().AnyAsync(value => value.MissionId == snapshot.MissionId &&
                    value.JobType == AiJobType.DiseaseDetection && value.Status == AiJobStatus.Completed, token) ||
                await db.Set<SurveyComboImportBatch>().AnyAsync(value => value.SurveyOrderId == order.Id, token))
                return PrepareFailure("DISEASE_RESULT_ALREADY_EXISTS", "This order already has disease results from its original capture.");

            if (!await IsDiseaseDroneAvailableAsync(request.DroneId, request.StartAt, request.EndAt, null, token))
                return PrepareFailure("DRONE_NOT_READY", "The selected drone is not operational or lacks disease capture capability.");

            mappingAttempt.BindMission(snapshot.MissionId);
            mappingAttempt.AcceptProcessing(
                new CaptureProcessingAcceptanceEvidence(order.TenantId, order.FarmId, order.Id, mappingAttempt.Id,
                    snapshot.MissionId, mappingAttempt.ProcessingJobReference, snapshot.Id.ToString("D")),
                now, "V3_CAPTURE_COORDINATOR", "Verified mapping capture completed before disease capture",
                request.ActorId, request.CorrelationId);
            // Release the active-attempt index before inserting the next attempt in the same transaction.
            await db.SaveChangesAsync(token);

            var appointment = SurveyAppointment.Create(order.Id, SurveyAppointmentPurpose.DiseaseCapture,
                1, null, request.ActorId, request.StartAt, request.EndAt, now);
            appointment.ConfirmDiseaseCaptureByManager(request.ActorId, now);
            var attemptId = Guid.CreateVersion7();
            using var flightParameters = JsonSerializer.SerializeToDocument(new
            {
                contract = "SURVEY_CAPTURE_V3_DISEASE",
                orderId = order.Id,
                captureAttemptId = attemptId,
                mappingSourceAttemptId = mappingAttempt.Id,
                MapsPoles = false,
                commercial.ScreensDisease,
                commercial.PolicyFingerprint
            });
            var mission = DroneMission.CreateOrderBound(
                order.Id, order.TenantId, order.FarmId, mappingMission.ScopeZoneIds, snapshot.FarmBoundaryVersionId,
                request.DroneId, request.ActorId, $"V3DIS-{order.Id:N}", MissionPurpose.Capture,
                request.OperationId, snapshot.FarmBaseMapVersionId, requiresBaselineCompletion: false,
                flightParameters, request.ActorId, now,
                mappingMission.ScopeType, mappingMission.ScopeSnapshot, mappingMission.ScopeFingerprint);
            var attempt = CaptureAttempt.CreateDiseaseCapture(attemptId, mappingAttempt, mission.Id, request.ActorId,
                request.CorrelationId, request.OperationId, fingerprint, now);
            mission.BindCaptureAttempt(attempt.Id);
            mission.Schedule(request.StartAt, request.EndAt, now);
            order.RecordDiseaseCaptureSchedule(now);
            db.Set<SurveyAppointment>().Add(appointment);
            db.Set<CaptureAttempt>().Add(attempt);
            db.Set<DroneMission>().Add(mission);
            AddCaptureEvidence(order, request.ActorId, request.CorrelationId,
                "DISEASE_CAPTURE_PREPARED", now, attempt.Id, mission.Id);
            await db.SaveChangesAsync(token);

            return new PrepareCaptureAttemptResult(true, attempt.Id, attempt.AttemptIndex, mission.Id,
                attempt.Status.ToString(), attempt.Version);
        },
        PrepareException,
        cancellationToken);

    public Task<PrepareCaptureAttemptResult> RescheduleDiseaseCaptureAsync(
        RescheduleDiseaseCaptureRequest request,
        CancellationToken cancellationToken = default) => ExecuteAsync(
        async token =>
        {
            await LockOrderAsync(request.OrderId, token);
            var order = await db.Set<SurveyOrder>().SingleOrDefaultAsync(value => value.Id == request.OrderId, token);
            if (order is null)
                return PrepareFailure("ORDER_NOT_FOUND", "Survey Order was not found.");
            if (!await IsAssignedManagerAsync(order.TenantId, order.FarmId, request.ActorId, token))
                return PrepareFailure("MANAGER_FORBIDDEN", "The actor is not the active assigned Manager.");

            var attempt = await db.Set<CaptureAttempt>().AsNoTracking().SingleOrDefaultAsync(value =>
                value.Id == request.AttemptId && value.OrderId == order.Id && value.TenantId == order.TenantId &&
                value.FarmId == order.FarmId && value.MappingSourceAttemptId != null &&
                value.Status == CaptureAttemptStatus.Planned, token);
            var mission = attempt is null ? null : await db.Set<DroneMission>().SingleOrDefaultAsync(value =>
                value.CaptureAttemptId == attempt.Id && value.SurveyOrderId == order.Id &&
                value.TenantId == order.TenantId && value.FarmId == order.FarmId &&
                value.Status == MissionStatus.Scheduled, token);
            if (attempt is null || mission is null || order.Status != SurveyOrderStatus.ReadyForCapture)
                return PrepareFailure("ORDER_NOT_READY", "Only a scheduled disease capture can be rescheduled.");

            var appointment = await db.Set<SurveyAppointment>().SingleOrDefaultAsync(value =>
                value.SurveyOrderId == order.Id && value.Purpose == SurveyAppointmentPurpose.DiseaseCapture &&
                value.Status == SurveyAppointmentStatus.Confirmed, token);
            if (appointment is null)
                return PrepareFailure("ORDER_NOT_READY", "The current confirmed disease appointment is missing.");
            if (appointment.ProposedStartAt == request.StartAt && appointment.ProposedEndAt == request.EndAt &&
                mission.DroneId == request.DroneId)
                return new PrepareCaptureAttemptResult(true, attempt.Id, attempt.AttemptIndex, mission.Id,
                    attempt.Status.ToString(), attempt.Version);

            var now = clock.GetUtcNow();
            if (order.Version != request.ExpectedOrderVersion)
                return PrepareFailure("ORDER_NOT_READY", "Reload the Order before rescheduling the disease capture.");
            if (request.DroneId == Guid.Empty || request.StartAt <= now || request.EndAt <= request.StartAt)
                return PrepareFailure("APPOINTMENT_WINDOW_INVALID", "A drone and future disease capture window are required.");
            if (!await IsDiseaseDroneAvailableAsync(request.DroneId, request.StartAt, request.EndAt, mission.Id, token))
                return PrepareFailure("DRONE_NOT_READY", "The selected drone is not available for the disease capture window.");

            if (mission.PreflightOperationId is Guid preflightOperationId)
            {
                var preflight = await db.Set<MissionPreflightChecklist>().SingleOrDefaultAsync(value =>
                    value.MissionId == mission.Id && value.ClientOperationId == preflightOperationId, token);
                preflight?.Supersede();
            }
            mission.Reschedule(request.StartAt, request.EndAt, request.DroneId, now);
            appointment.Cancel(now);
            await db.SaveChangesAsync(token);

            var revised = SurveyAppointment.Create(order.Id, SurveyAppointmentPurpose.DiseaseCapture,
                appointment.RevisionNumber + 1, appointment.Id, request.ActorId, request.StartAt, request.EndAt, now);
            revised.ConfirmDiseaseCaptureByManager(request.ActorId, now);
            db.Set<SurveyAppointment>().Add(revised);
            order.RecordDiseaseCaptureSchedule(now);
            AddCaptureEvidence(order, request.ActorId, request.CorrelationId,
                "DISEASE_CAPTURE_RESCHEDULED", now, attempt.Id, mission.Id);
            await db.SaveChangesAsync(token);

            return new PrepareCaptureAttemptResult(true, attempt.Id, attempt.AttemptIndex, mission.Id,
                attempt.Status.ToString(), attempt.Version);
        },
        PrepareException,
        cancellationToken);

    private async Task<bool> IsDiseaseDroneAvailableAsync(
        Guid droneId, DateTimeOffset startAt, DateTimeOffset endAt, Guid? excludedMissionId, CancellationToken token)
    {
        await LockDroneAsync(droneId, token);
        var drone = await db.Set<Drone>().SingleOrDefaultAsync(value => value.Id == droneId, token);
        return drone is not null && drone.IsOperationalFor(startAt, endAt) &&
            drone.SupportsPurpose(MissionPurpose.PlantHealth) &&
            !await HasOverlappingDroneMissionAsync(droneId, startAt, endAt, excludedMissionId, token);
    }

    private Task<bool> HasOverlappingDroneMissionAsync(
        Guid droneId, DateTimeOffset startAt, DateTimeOffset endAt, Guid? excludedMissionId, CancellationToken token) =>
        db.Set<DroneMission>().AnyAsync(value => value.DroneId == droneId &&
            (!excludedMissionId.HasValue || value.Id != excludedMissionId.Value) &&
            (value.Status == MissionStatus.Scheduled || value.Status == MissionStatus.InFlight) &&
            value.ScheduledAt < endAt && value.ScheduledEndAt > startAt, token);

    public Task<StartCaptureFlightResult> StartFlightAsync(
        StartCaptureFlightRequest request,
        CancellationToken cancellationToken = default) => ExecuteAsync(
        async token =>
        {
            await LockOrderAsync(request.OrderId, token);
            var now = clock.GetUtcNow();
            var order = await db.Set<SurveyOrder>()
                .SingleOrDefaultAsync(value => value.Id == request.OrderId, token);
            var attempt = await db.Set<CaptureAttempt>()
                .Include(value => value.History)
                .SingleOrDefaultAsync(value =>
                    value.Id == request.AttemptId &&
                    value.OrderId == request.OrderId,
                    token);
            var mission = await db.Set<DroneMission>()
                .SingleOrDefaultAsync(value =>
                    value.Id == request.MissionId &&
                    value.CaptureAttemptId == request.AttemptId &&
                    value.SurveyOrderId == request.OrderId,
                    token);
            if (order is null || attempt is null || mission is null)
            {
                return StartFailure("CAPTURE_GRAPH_NOT_FOUND", "The Order, Attempt, or Mission binding was not found.");
            }
            await _probe.HitAsync(SurveyCaptureCheckpoint.AfterAttemptAndMissionTracked, token);
            if (!await IsAssignedManagerAsync(
                    order.TenantId,
                    order.FarmId,
                    request.ActorId,
                    token))
            {
                return StartFailure("MANAGER_FORBIDDEN", "The actor is not the active assigned Manager.");
            }
            var replayFingerprint = await ReadOperationFingerprintAsync(
                attempt.Id, request.OperationId, token);
            if (replayFingerprint is not null)
            {
                return string.Equals(replayFingerprint, request.PayloadFingerprint, StringComparison.Ordinal)
                    ? new StartCaptureFlightResult(
                        true, attempt.Id, attempt.Status.ToString(), attempt.Version,
                        attempt.StartedAt ?? attempt.UpdatedAt)
                    : StartFailure("OPERATION_CONFLICT", "The operation identifier was already used with another payload.");
            }
            if (order.Scope != ContractScope.V3 ||
                order.Status != SurveyOrderStatus.ReadyForCapture ||
                order.Version != request.ExpectedOrderVersion ||
                attempt.Version != request.ExpectedAttemptVersion)
            {
                return StartFailure("CAPTURE_VERSION_CONFLICT", "Capture state or expected version is stale.");
            }
            var appointmentPurpose = attempt.MappingSourceAttemptId.HasValue
                ? SurveyAppointmentPurpose.DiseaseCapture
                : SurveyAppointmentPurpose.Capture;
            var appointment = await db.Set<SurveyAppointment>()
                .AsNoTracking()
                .SingleOrDefaultAsync(value =>
                    value.SurveyOrderId == order.Id &&
                    value.Purpose == appointmentPurpose &&
                    value.Status == SurveyAppointmentStatus.Confirmed,
                    token);
            var obligation = order.DepositObligationId.HasValue
                ? await db.Set<SurveyDepositObligation>()
                    .AsNoTracking()
                    .SingleOrDefaultAsync(value =>
                        value.Id == order.DepositObligationId.Value &&
                        value.SurveyOrderId == order.Id,
                        token)
                : null;
            if (appointment is null || now < appointment.ProposedStartAt ||
                now >= appointment.ProposedEndAt || obligation?.IsSatisfied != true)
            {
                return StartFailure("CAPTURE_GATE_CLOSED", "Appointment window or deposit gate is not current.");
            }

            await LockDroneAsync(mission.DroneId, token);
            var drone = await db.Set<Drone>().SingleAsync(
                value => value.Id == mission.DroneId,
                token);
            if (!drone.IsOperationalFor(now, appointment.ProposedEndAt) ||
                mission.PreflightSuitableForFlight != true ||
                mission.PreflightOperationId is null)
            {
                return StartFailure("PREFLIGHT_OR_DRONE_NOT_READY", "Current drone and immutable preflight evidence are required.");
            }

            attempt.BindMission(mission.Id);
            if (attempt.Status == CaptureAttemptStatus.Planned)
            {
                attempt.MarkPreflightReady(
                    $"MISSION_PREFLIGHT:{mission.PreflightOperationId}",
                    request.ActorId,
                    "V3_CAPTURE_COORDINATOR",
                    "Validated immutable Mission preflight evidence",
                    request.CorrelationId,
                    operationId: null,
                    payloadFingerprint: null,
                    timestamp: now,
                    expectedVersion: request.ExpectedAttemptVersion);
            }
            attempt.StartFlight(
                mission.Id,
                now,
                "V3_CAPTURE_COORDINATOR",
                "Atomic capture start",
                $"MISSION_PREFLIGHT:{mission.PreflightOperationId}",
                request.ActorId,
                request.CorrelationId,
                request.OperationId,
                request.PayloadFingerprint,
                attempt.Version);
            mission.StartFlight(request.ActorId, now);
            drone.StartMission(now);
            order.TransitionToCapturingV3(now);
            await _probe.HitAsync(SurveyCaptureCheckpoint.AfterStateTransitionsTracked, token);
            AddCaptureEvidence(order, request.ActorId, request.CorrelationId,
                "CAPTURE_STARTED", now, attempt.Id, mission.Id);
            await _probe.HitAsync(SurveyCaptureCheckpoint.AfterAuditAndOutboxTracked, token);
            await db.SaveChangesAsync(token);
            return new StartCaptureFlightResult(
                true,
                attempt.Id,
                attempt.Status.ToString(),
                attempt.Version,
                now);
        },
        StartException,
        cancellationToken);

    public Task<EndCaptureFlightResult> EndFlightAsync(
        EndCaptureFlightRequest request,
        CancellationToken cancellationToken = default) => ExecuteAsync(
        async token =>
        {
            await LockOrderAsync(request.OrderId, token);
            var now = clock.GetUtcNow();
            if (request.EndedAt > now)
            {
                return EndFailure("FUTURE_FLIGHT_EVIDENCE", "Flight completion time cannot be in the future.");
            }
            var order = await db.Set<SurveyOrder>().SingleOrDefaultAsync(
                value => value.Id == request.OrderId, token);
            var attempt = await db.Set<CaptureAttempt>()
                .Include(value => value.History)
                .SingleOrDefaultAsync(value => value.Id == request.AttemptId, token);
            var mission = attempt is null
                ? null
                : await db.Set<DroneMission>().SingleOrDefaultAsync(value =>
                    value.CaptureAttemptId == attempt.Id &&
                    value.SurveyOrderId == request.OrderId,
                    token);
            if (order is null || attempt is null || mission is null ||
                order.Scope != ContractScope.V3)
            {
                return EndFailure("CAPTURE_VERSION_CONFLICT", "Capture graph is missing or stale.");
            }
            if (!await IsAssignedManagerAsync(
                    order.TenantId,
                    order.FarmId,
                    request.ActorId,
                    token))
            {
                return EndFailure("MANAGER_FORBIDDEN", "The actor is not the active assigned Manager.");
            }
            var replayFingerprint = await ReadOperationFingerprintAsync(
                attempt.Id, request.OperationId, token);
            if (replayFingerprint is not null)
            {
                return string.Equals(replayFingerprint, request.PayloadFingerprint, StringComparison.Ordinal)
                    ? new EndCaptureFlightResult(
                        true, attempt.Id, attempt.Status.ToString(), attempt.Version,
                        attempt.EndedAt ?? attempt.UpdatedAt)
                    : EndFailure("OPERATION_CONFLICT", "The operation identifier was already used with another payload.");
            }
            if (
                order.Status != SurveyOrderStatus.Capturing ||
                order.Version != request.ExpectedOrderVersion ||
                attempt.Version != request.ExpectedAttemptVersion)
            {
                return EndFailure("CAPTURE_VERSION_CONFLICT", "Capture graph is missing or stale.");
            }
            await LockDroneAsync(mission.DroneId, token);
            var drone = await db.Set<Drone>().SingleAsync(value => value.Id == mission.DroneId, token);
            attempt.BindMission(mission.Id);
            attempt.RecordFlightCompleted(
                request.EndedAt,
                request.FlightCompletedReceipt,
                "V3_CAPTURE_COORDINATOR",
                "Flight completed",
                request.ActorId,
                request.CorrelationId,
                request.OperationId,
                request.PayloadFingerprint,
                request.ExpectedAttemptVersion);
            mission.CompleteFlight(request.EndedAt);
            drone.CompleteMission(request.EndedAt);
            order.TransitionToAwaitingCaptureUploadV3(request.EndedAt);
            AddCaptureEvidence(order, request.ActorId, request.CorrelationId,
                "CAPTURE_FLIGHT_COMPLETED", request.EndedAt, attempt.Id, mission.Id);
            await db.SaveChangesAsync(token);
            return new EndCaptureFlightResult(
                true,
                attempt.Id,
                attempt.Status.ToString(),
                attempt.Version,
                request.EndedAt);
        },
        EndException,
        cancellationToken);

    public Task<RecordFlightFailureResult> RecordFlightFailureAsync(
        RecordFlightFailureRequest request,
        CancellationToken cancellationToken = default) => ExecuteAsync(
        async token =>
        {
            await LockOrderAsync(request.OrderId, token);
            var now = clock.GetUtcNow();
            if (request.FailedAt > now)
            {
                return FailureFailure("FUTURE_FLIGHT_EVIDENCE", "Flight failure time cannot be in the future.");
            }
            var order = await db.Set<SurveyOrder>().SingleOrDefaultAsync(
                value => value.Id == request.OrderId, token);
            var attempt = await db.Set<CaptureAttempt>()
                .Include(value => value.History)
                .SingleOrDefaultAsync(value => value.Id == request.AttemptId, token);
            var mission = attempt is null
                ? null
                : await db.Set<DroneMission>().SingleOrDefaultAsync(value =>
                    value.CaptureAttemptId == attempt.Id &&
                    value.SurveyOrderId == request.OrderId,
                    token);
            if (order is null || attempt is null || mission is null ||
                order.Scope != ContractScope.V3)
            {
                return FailureFailure("CAPTURE_VERSION_CONFLICT", "Capture graph is missing or stale.");
            }
            if (!await IsAssignedManagerAsync(
                    order.TenantId,
                    order.FarmId,
                    request.ActorId,
                    token))
            {
                return FailureFailure("MANAGER_FORBIDDEN", "The actor is not the active assigned Manager.");
            }
            var replayFingerprint = await ReadOperationFingerprintAsync(
                attempt.Id, request.OperationId, token);
            if (replayFingerprint is not null)
            {
                return string.Equals(replayFingerprint, request.PayloadFingerprint, StringComparison.Ordinal)
                    ? new RecordFlightFailureResult(
                        true, attempt.Id, attempt.Status.ToString(), attempt.Version)
                    : FailureFailure("OPERATION_CONFLICT", "The operation identifier was already used with another payload.");
            }
            if (
                order.Status != SurveyOrderStatus.Capturing ||
                order.Version != request.ExpectedOrderVersion ||
                attempt.Version != request.ExpectedAttemptVersion)
            {
                return FailureFailure("CAPTURE_VERSION_CONFLICT", "Capture graph is missing or stale.");
            }
            await LockDroneAsync(mission.DroneId, token);
            var drone = await db.Set<Drone>().SingleAsync(value => value.Id == mission.DroneId, token);
            attempt.BindMission(mission.Id);
            attempt.RecordFlightFailed(
                request.FailedAt,
                request.FailureReason,
                request.FailureIncidentEvidence,
                "V3_CAPTURE_COORDINATOR",
                request.ActorId,
                request.CorrelationId,
                request.OperationId,
                request.PayloadFingerprint,
                request.ExpectedAttemptVersion);
            mission.FailFlight(request.FailedAt);
            drone.FailMission(request.FailedAt);
            order.TransitionToAwaitingReflightDecisionV3(request.FailedAt);
            AddCaptureEvidence(order, request.ActorId, request.CorrelationId,
                "CAPTURE_FLIGHT_FAILED", request.FailedAt, attempt.Id, mission.Id);
            await db.SaveChangesAsync(token);
            return new RecordFlightFailureResult(
                true,
                attempt.Id,
                attempt.Status.ToString(),
                attempt.Version);
        },
        FailureException,
        cancellationToken);

    public Task<ReopenCaptureInputManifestResult> ReopenInputManifestAsync(
        ReopenCaptureInputManifestRequest request,
        CancellationToken cancellationToken = default) => ExecuteAsync(
        async token =>
        {
            await LockOrderAsync(request.OrderId, token);
            var order = await db.Set<SurveyOrder>().SingleOrDefaultAsync(
                value => value.Id == request.OrderId && value.Scope == ContractScope.V3, token);
            if (order is null)
                return ReopenFailure("CAPTURE_MANIFEST_NOT_FOUND", "V3 order or manifest was not found.");

            if (!await IsAssignedManagerAsync(order.TenantId, order.FarmId, request.ActorId, token))
                return ReopenFailure("MANAGER_FORBIDDEN", "The actor is not the active assigned Manager.");

            var attempt = await db.Set<CaptureAttempt>()
                .Include(value => value.History)
                .SingleOrDefaultAsync(value => value.Id == request.AttemptId && value.OrderId == order.Id
                    && value.TenantId == order.TenantId && value.FarmId == order.FarmId, token);
            var manifest = await db.Set<CaptureInputManifest>()
                .SingleOrDefaultAsync(value => value.Id == request.ManifestId
                    && value.CaptureAttemptId == request.AttemptId && value.SurveyOrderId == order.Id
                    && value.TenantId == order.TenantId && value.FarmId == order.FarmId, token);
            if (attempt is null || manifest is null)
                return ReopenFailure("CAPTURE_MANIFEST_NOT_FOUND", "V3 order or manifest was not found.");

            var reopenAction = request.ReplaceApprovedInput
                ? CaptureAttemptAction.ReopenApprovedUpload : CaptureAttemptAction.ReopenUpload;
            var replay = attempt.History.SingleOrDefault(value => value.OperationId == request.OperationId);
            if (replay is not null)
            {
                return replay.Action == reopenAction &&
                       replay.EvidenceReference == manifest.Id.ToString("D") &&
                       string.Equals(replay.PayloadFingerprint, request.PayloadFingerprint.Trim(), StringComparison.Ordinal)
                    ? new ReopenCaptureInputManifestResult(
                        true, attempt.Id, manifest.MissionId, manifest.Id, attempt.Status.ToString(),
                        attempt.Version, order.Version)
                    : ReopenFailure("OPERATION_CONFLICT", "The operation identifier was already used with another payload.");
            }

            if (order.Version != request.ExpectedOrderVersion ||
                attempt.Version != request.ExpectedAttemptVersion ||
                attempt.UploadManifestReference != manifest.Id.ToString("D") ||
                attempt.UploadManifestFingerprint != manifest.ManifestFingerprint)
            {
                return ReopenFailure("CAPTURE_MANIFEST_STATE_CONFLICT", "The current order, attempt or manifest has changed.");
            }

            if (request.ReplaceApprovedInput)
                return await ReopenApprovedDiseaseInputAsync(request, order, attempt, manifest, token);

            if (order.Status != SurveyOrderStatus.AwaitingInputReview ||
                attempt.Status != CaptureAttemptStatus.Uploaded ||
                manifest.Status != CaptureInputManifestStatus.Uploaded ||
                await db.Set<CaptureInputApproval>().AnyAsync(value => value.InputManifestId == manifest.Id, token))
            {
                return ReopenFailure("CAPTURE_MANIFEST_STATE_CONFLICT", "Only the current unapproved manifest can be reopened.");
            }

            var mission = await db.Set<DroneMission>().SingleOrDefaultAsync(value =>
                value.Id == manifest.MissionId && value.CaptureAttemptId == attempt.Id &&
                value.SurveyOrderId == order.Id && value.TenantId == order.TenantId && value.FarmId == order.FarmId, token);
            if (mission is null ||
                mission.Status is not (MissionStatus.FlightCompleted or MissionStatus.Uploading or MissionStatus.UploadFailed))
            {
                return ReopenFailure("CAPTURE_MISSION_STATE_CONFLICT", "The mission has left the upload phase.");
            }

            var job = await FindCaptureMappingJob(manifest).SingleOrDefaultAsync(token);
            if (job is not null && job.Status != AiJobStatus.Queued)
                return ReopenFailure("CAPTURE_JOB_STATE_CONFLICT", "The manifest's processing job has already advanced.");

            var now = clock.GetUtcNow();
            if (mission.Status == MissionStatus.UploadFailed)
                mission.StartUploading(now);
            job?.CancelQueuedCaptureMapping(now);
            manifest.MarkSuperseded(now);
            attempt.ReopenUpload(manifest.Id, now, request.ActorId, request.CorrelationId, request.OperationId,
                request.PayloadFingerprint, request.Reason, request.ExpectedAttemptVersion);
            order.TransitionToAwaitingCaptureUploadV3(now);
            AddCaptureEvidence(order, request.ActorId, request.CorrelationId,
                "CAPTURE_INPUT_MANIFEST_REOPENED", now, attempt.Id, mission.Id);
            await db.SaveChangesAsync(token);

            return new ReopenCaptureInputManifestResult(
                true, attempt.Id, mission.Id, manifest.Id, attempt.Status.ToString(), attempt.Version, order.Version);
        },
        exception => ReopenFailure("CAPTURE_MANIFEST_REOPEN_CONFLICT", GetErrorMessage(exception)),
        cancellationToken,
        request.ReplaceApprovedInput ? IsolationLevel.ReadCommitted : IsolationLevel.Serializable);

    private async Task<ReopenCaptureInputManifestResult> ReopenApprovedDiseaseInputAsync(
        ReopenCaptureInputManifestRequest request,
        SurveyOrder order,
        CaptureAttempt attempt,
        CaptureInputManifest manifest,
        CancellationToken token)
    {
        if (order.Status != SurveyOrderStatus.Processing || order.CurrentApprovedResultRevisionId.HasValue ||
            order.CurrentInvoiceRevisionId.HasValue || !attempt.MappingSourceAttemptId.HasValue ||
            attempt.Status != CaptureAttemptStatus.Processing || manifest.Status != CaptureInputManifestStatus.Approved ||
            !Guid.TryParse(attempt.InputApprovalReference, out var approvalId) ||
            !Guid.TryParse(attempt.ProcessingJobReference, out var processingJobId))
        {
            return ReopenFailure("CAPTURE_MANIFEST_STATE_CONFLICT",
                "Only the current approved input of a processing disease capture can be replaced.");
        }

        // Job submission and the preliminary-result publisher use the same mission lock.
        await LockRowAsync("SELECT id FROM mission.drone_missions WHERE id = @id FOR UPDATE", manifest.MissionId, token);
        var mission = await db.Set<DroneMission>().SingleOrDefaultAsync(value =>
            value.Id == manifest.MissionId && value.CaptureAttemptId == attempt.Id && value.SurveyOrderId == order.Id &&
            value.TenantId == order.TenantId && value.FarmId == order.FarmId, token);
        if (mission is null || mission.Status != MissionStatus.ReadyForProcessing)
            return ReopenFailure("CAPTURE_MISSION_STATE_CONFLICT", "The disease mission must be ready for processing.");

        var approvalExists = await db.Set<CaptureInputApproval>().AnyAsync(value =>
            value.Id == approvalId && value.InputManifestId == manifest.Id && value.CaptureAttemptId == attempt.Id &&
            value.ProcessingJobId == processingJobId && value.ManifestFingerprint == manifest.ManifestFingerprint, token);
        if (!approvalExists)
            return ReopenFailure("CAPTURE_MANIFEST_STATE_CONFLICT", "The current input approval could not be verified.");

        var partitionKey = mission.Id.ToString("D");
        if (await db.Set<OutboxMessage>().AnyAsync(value =>
                value.TenantId == order.TenantId && value.PartitionKey == partitionKey &&
                value.EventType == IntegrationEventTypes.ComboAnalysisReadyV1, token) ||
            await db.Set<SurveyComboImportBatch>().AnyAsync(value =>
                value.SurveyOrderId == order.Id && value.MissionId == mission.Id, token))
        {
            return ReopenFailure("CAPTURE_DISEASE_RESULT_ALREADY_SENT",
                "The disease result has already been submitted for review.");
        }

        var jobsActive = await db.Database.SqlQuery<bool>($"""
            SELECT EXISTS (SELECT 1 FROM mission.tiling_jobs
                WHERE mission_id = {mission.Id} AND tenant_id = {order.TenantId} AND farm_id = {order.FarmId}
                  AND status IN ('QUEUED', 'RUNNING')) AS "Value"
            """).SingleAsync(token);
        if (jobsActive)
            return ReopenFailure("CAPTURE_JOB_STATE_CONFLICT", "Wait for the active mission job to finish before replacing input.");

        await LockRowAsync("SELECT id FROM mission.ai_processing_jobs WHERE id = @id FOR UPDATE", processingJobId, token);
        var job = await FindCaptureMappingJob(manifest).SingleOrDefaultAsync(token);
        if (job is null || job.Id != processingJobId || job.Status != AiJobStatus.Queued)
            return ReopenFailure("CAPTURE_JOB_STATE_CONFLICT", "The manifest's processing job has already advanced.");

        var telemetry = await ReadValidCaptureTelemetryAsync(order, mission, token);
        if (telemetry is null || FormatTelemetryEvidence(telemetry) != manifest.GpsMetadataSummary)
            return ReopenFailure("CAPTURE_TELEMETRY_NOT_FROZEN", "The approved telemetry could not be archived.");

        var points = await db.Set<MissionTelemetryPoint>().AsNoTracking()
            .Where(point => point.MissionId == mission.Id).OrderBy(point => point.SequenceNumber)
            .Select(point => new
            {
                point.SequenceNumber, point.RecordedAt,
                Longitude = point.Location.X, Latitude = point.Location.Y,
                point.AltitudeM, point.AltitudeReference, point.HeadingDeg, point.SpeedMps, point.HorizontalAccuracyM
            }).ToListAsync(token);

        var now = clock.GetUtcNow();
        mission.ReopenCaptureUpload(request.OperationId, now);
        job.CancelQueuedCaptureMapping(now);
        manifest.SupersedeApprovedInput();
        attempt.ReopenUpload(manifest.Id, now, request.ActorId, request.CorrelationId, request.OperationId,
            request.PayloadFingerprint, request.Reason, request.ExpectedAttemptVersion, replaceApprovedInput: true);
        order.ReopenApprovedDiseaseCaptureUpload(now);
        using var oldData = JsonSerializer.SerializeToDocument(new
        {
            manifestId = manifest.Id,
            approvalId,
            telemetryId = telemetry.Id,
            telemetry.UploadRevisionId,
            telemetry.OperationId,
            telemetry.SourceFileName,
            telemetry.PayloadChecksum,
            telemetry.ImportedAt,
            points
        });
        AddCaptureEvidence(order, request.ActorId, request.CorrelationId,
            "CAPTURE_DISEASE_INPUT_REOPENED", now, attempt.Id, mission.Id, oldData: oldData);
        await db.SaveChangesAsync(token);

        return new ReopenCaptureInputManifestResult(
            true, attempt.Id, mission.Id, manifest.Id, attempt.Status.ToString(), attempt.Version, order.Version);
    }

    public Task<ApproveCaptureInputResult> ApproveInputAsync(
        ApproveCaptureInputRequest request,
        CancellationToken cancellationToken = default) => ExecuteAsync(
        async token =>
        {
            await LockOrderAsync(request.OrderId, token);
            var now = clock.GetUtcNow();
            var order = await db.Set<SurveyOrder>().SingleOrDefaultAsync(
                value => value.Id == request.OrderId, token);
            var attempt = await db.Set<CaptureAttempt>()
                .Include(value => value.History)
                .SingleOrDefaultAsync(value => value.Id == request.AttemptId && value.OrderId == request.OrderId, token);
            var manifest = await db.Set<CaptureInputManifest>()
                .SingleOrDefaultAsync(value =>
                    value.Id == request.ManifestId &&
                    value.CaptureAttemptId == request.AttemptId &&
                    value.SurveyOrderId == request.OrderId,
                    token);
            var mission = attempt is null
                ? null
                : await db.Set<DroneMission>().SingleOrDefaultAsync(
                    value => value.CaptureAttemptId == attempt.Id && value.SurveyOrderId == request.OrderId, token);
            if (order is null || attempt is null || manifest is null || mission is null ||
                order.Scope != ContractScope.V3 || mission.Purpose != MissionPurpose.Capture ||
                mission.Id != manifest.MissionId ||
                mission.TenantId != order.TenantId || mission.FarmId != order.FarmId)
            {
                return ApprovalFailure("INPUT_VERSION_CONFLICT", "Uploaded manifest, Attempt, or Order is stale.");
            }
            if (!await IsAssignedManagerAsync(
                    order.TenantId,
                    order.FarmId,
                    request.ActorId,
                    token))
            {
                return ApprovalFailure("MANAGER_FORBIDDEN", "The actor is not the active assigned Manager.");
            }
            var replay = attempt.History.SingleOrDefault(value => value.OperationId == request.OperationId);
            if (replay is not null)
            {
                var replayApprovalId = await db.Set<CaptureInputApproval>()
                    .AsNoTracking()
                    .Where(value =>
                        value.CaptureAttemptId == attempt.Id &&
                        value.InputManifestId == manifest.Id &&
                        value.ProcessingJobId == request.ProcessingJobId &&
                        value.ManifestFingerprint == request.ManifestFingerprint)
                    .Select(value => value.Id)
                    .SingleOrDefaultAsync(token);
                return replay.Action == CaptureAttemptAction.StartProcessing &&
                       string.Equals(replay.PayloadFingerprint, request.PayloadFingerprint.Trim(), StringComparison.Ordinal) &&
                       replayApprovalId != Guid.Empty
                    ? new ApproveCaptureInputResult(
                        true, attempt.Id, replayApprovalId, attempt.Status.ToString(), attempt.Version)
                    : ApprovalFailure("OPERATION_CONFLICT", "The operation identifier was already used with another payload.");
            }
            if (order.Scope != ContractScope.V3 ||
                order.Status != SurveyOrderStatus.AwaitingInputReview ||
                order.Version != request.ExpectedOrderVersion ||
                attempt.Version != request.ExpectedAttemptVersion ||
                attempt.Status != CaptureAttemptStatus.Uploaded ||
                manifest.Status != CaptureInputManifestStatus.Uploaded ||
                attempt.UploadManifestReference != manifest.Id.ToString("D") ||
                !string.Equals(manifest.ManifestFingerprint, request.ManifestFingerprint, StringComparison.Ordinal))
            {
                return ApprovalFailure("INPUT_VERSION_CONFLICT", "Uploaded Attempt or exact manifest is missing or stale.");
            }

            if (mission.Status is not (MissionStatus.Uploading or MissionStatus.ReadyForProcessing))
            {
                return ApprovalFailure("CAPTURE_MISSION_STATE_CONFLICT",
                    "Only an uploading mission or a mission already ready for processing can be approved.");
            }

            var telemetry = await ReadValidCaptureTelemetryAsync(order, mission, token);
            if (telemetry is null || manifest.GpsMetadataSummary != FormatTelemetryEvidence(telemetry))
            {
                return ApprovalFailure("CAPTURE_TELEMETRY_NOT_FROZEN",
                    "Reopen the manifest, import valid Blackbox telemetry and freeze it with the photos before approval.");
            }

            var jobId = await FindCaptureMappingJob(manifest).AsNoTracking()
                .Where(value => value.Status == AiJobStatus.Queued)
                .Select(value => (Guid?)value.Id)
                .SingleOrDefaultAsync(token);
            if (jobId is null || jobId != request.ProcessingJobId)
            {
                return ApprovalFailure("CAPTURE_PROCESSING_JOB_MISMATCH",
                    "Use the processingJobId associated with the current manifest.");
            }

            attempt.BindMission(mission.Id);
            var approval = CaptureInputApproval.Create(
                Guid.CreateVersion7(),
                order.TenantId,
                order.FarmId,
                order.Id,
                attempt.Id,
                mission.Id,
                manifest.Id,
                manifest.ManifestFingerprint,
                request.ProcessingJobId,
                request.ActorId,
                now,
                request.Notes);
            manifest.MarkApproved(now);
            var evidence = new CaptureInputApprovalEvidence(
                order.TenantId,
                order.FarmId,
                order.Id,
                attempt.Id,
                mission.Id,
                manifest.Id.ToString("D"),
                manifest.ManifestFingerprint,
                approval.Id.ToString("D"),
                request.ProcessingJobId.ToString("D"));
            attempt.StartProcessing(
                evidence,
                now,
                "V3_CAPTURE_COORDINATOR",
                "Exact capture input manifest approved",
                request.ActorId,
                request.CorrelationId,
                request.OperationId,
                request.PayloadFingerprint,
                request.ExpectedAttemptVersion);
            if (order.Status == SurveyOrderStatus.AwaitingInputReview)
            {
                order.TransitionToAwaitingProcessingV3(now);
                order.TransitionToProcessingV3(now);
            }
            if (mission.Status == MissionStatus.Uploading)
                mission.MarkReadyForProcessing(now);

            db.Set<CaptureInputApproval>().Add(approval);
            AddCaptureEvidence(order, request.ActorId, request.CorrelationId,
                "CAPTURE_INPUT_APPROVED", now, attempt.Id, mission.Id);
            await db.SaveChangesAsync(token);
            return new ApproveCaptureInputResult(
                true,
                attempt.Id,
                approval.Id,
                attempt.Status.ToString(),
                attempt.Version);
        },
        ApprovalException,
        cancellationToken);

    public Task<RecoverCaptureMissionResult> RecoverApprovedMissionAsync(
        RecoverCaptureMissionRequest request,
        CancellationToken cancellationToken = default) => ExecuteAsync(
        async token =>
        {
            await LockOrderAsync(request.OrderId, token);
            var order = await db.Set<SurveyOrder>().AsNoTracking().SingleOrDefaultAsync(
                value => value.Id == request.OrderId && value.Scope == ContractScope.V3, token);
            if (order is null)
                return RecoverMissionFailure("CAPTURE_NOT_FOUND", "V3 order or capture attempt was not found.");

            if (!await IsAssignedManagerAsync(order.TenantId, order.FarmId, request.ActorId, token))
                return RecoverMissionFailure("MANAGER_FORBIDDEN", "The actor is not the active assigned Manager.");

            var attempt = await db.Set<CaptureAttempt>().AsNoTracking().SingleOrDefaultAsync(
                value => value.Id == request.AttemptId && value.OrderId == order.Id &&
                    value.TenantId == order.TenantId && value.FarmId == order.FarmId, token);
            if (attempt is null)
                return RecoverMissionFailure("CAPTURE_NOT_FOUND", "V3 order or capture attempt was not found.");

            if (order.Status != SurveyOrderStatus.Processing || attempt.Status != CaptureAttemptStatus.Processing ||
                !Guid.TryParse(attempt.InputApprovalReference, out var approvalId) ||
                !Guid.TryParse(attempt.UploadManifestReference, out var manifestId) ||
                !Guid.TryParse(attempt.ProcessingJobReference, out var jobId))
            {
                return RecoverMissionFailure("CAPTURE_APPROVAL_NOT_CURRENT",
                    "Recovery requires a Processing order and attempt with a current input approval.");
            }

            var mission = await db.Set<DroneMission>().SingleOrDefaultAsync(value =>
                value.CaptureAttemptId == attempt.Id && value.SurveyOrderId == order.Id &&
                value.TenantId == order.TenantId && value.FarmId == order.FarmId &&
                value.Purpose == MissionPurpose.Capture, token);
            if (mission is null)
                return RecoverMissionFailure("CAPTURE_NOT_FOUND", "The mission bound to this capture attempt was not found.");

            var hasCurrentApproval = await (
                from approval in db.Set<CaptureInputApproval>()
                join manifest in db.Set<CaptureInputManifest>() on approval.InputManifestId equals manifest.Id
                join job in db.Set<AiProcessingJob>() on approval.ProcessingJobId equals job.Id
                where approval.Id == approvalId && approval.CaptureAttemptId == attempt.Id &&
                      approval.TenantId == order.TenantId && approval.FarmId == order.FarmId &&
                      approval.SurveyOrderId == order.Id && approval.MissionId == mission.Id &&
                      manifest.Id == manifestId && manifest.Status == CaptureInputManifestStatus.Approved &&
                      manifest.ManifestFingerprint == attempt.UploadManifestFingerprint &&
                      approval.ManifestFingerprint == manifest.ManifestFingerprint &&
                      job.Id == jobId && job.MissionId == mission.Id
                select approval.Id).AnyAsync(token);
            if (!hasCurrentApproval)
            {
                return RecoverMissionFailure("CAPTURE_APPROVAL_NOT_CURRENT",
                    "The current approval, approved manifest, and processing job do not match the capture mission.");
            }

            var alreadyReady = mission.Status == MissionStatus.ReadyForProcessing;
            if (!alreadyReady)
            {
                if (mission.Status != MissionStatus.Uploading)
                    return RecoverMissionFailure("CAPTURE_MISSION_STATE_CONFLICT", "Only an uploading mission can be recovered.");

                var now = clock.GetUtcNow();
                mission.MarkReadyForProcessing(now);
                AddCaptureEvidence(order, request.ActorId, request.CorrelationId,
                    "CAPTURE_MISSION_PROCESSING_READINESS_RECOVERED", now, attempt.Id, mission.Id);
                await db.SaveChangesAsync(token);
            }

            return new RecoverCaptureMissionResult(
                true, attempt.Id, mission.Id, approvalId, manifestId, jobId, mission.Version, alreadyReady);
        },
        exception => RecoverMissionFailure("CAPTURE_MISSION_RECOVERY_CONFLICT", GetErrorMessage(exception)),
        cancellationToken);

    public Task<FreezeCaptureInputManifestResult> FreezeInputManifestAsync(
        FreezeCaptureInputManifestRequest request,
        CancellationToken cancellationToken = default) => ExecuteAsync(
        async token =>
        {
            await LockOrderAsync(request.OrderId, token);
            var now = clock.GetUtcNow();
            var order = await db.Set<SurveyOrder>().SingleOrDefaultAsync(
                value => value.Id == request.OrderId, token);
            var attempt = await db.Set<CaptureAttempt>()
                .Include(value => value.History)
                .SingleOrDefaultAsync(value =>
                    value.Id == request.AttemptId &&
                    value.OrderId == request.OrderId,
                    token);
            var mission = await db.Set<DroneMission>().AsNoTracking()
                .SingleOrDefaultAsync(value =>
                    value.Id == request.MissionId &&
                    value.CaptureAttemptId == request.AttemptId &&
                    value.SurveyOrderId == request.OrderId,
                    token);
            if (order is null || attempt is null || mission is null ||
                order.Scope != ContractScope.V3 || mission.TenantId != order.TenantId || mission.FarmId != order.FarmId)
            {
                return ManifestFailure(
                    "CAPTURE_UPLOAD_VERSION_CONFLICT",
                    "The V3 capture graph is missing or stale.");
            }
            if (!await IsAssignedManagerAsync(
                    order.TenantId,
                    order.FarmId,
                    request.ActorId,
                    token))
            {
                return ManifestFailure("MANAGER_FORBIDDEN", "The actor is not the active assigned Manager.");
            }
            var replay = attempt.History.SingleOrDefault(value => value.OperationId == request.OperationId);
            if (replay is not null)
            {
                if (replay.Action != CaptureAttemptAction.MarkUploaded ||
                    !string.Equals(replay.PayloadFingerprint, request.PayloadFingerprint.Trim(), StringComparison.Ordinal) ||
                    replay.EvidenceReference is not { Length: >= 36 } reference ||
                    !Guid.TryParse(reference.AsSpan(0, 36), out var manifestId))
                {
                    return ManifestFailure("OPERATION_CONFLICT", "The operation identifier was already used with another payload.");
                }

                var replayManifest = await db.Set<CaptureInputManifest>().AsNoTracking()
                    .SingleOrDefaultAsync(value => value.Id == manifestId &&
                        value.CaptureAttemptId == attempt.Id && value.MissionId == mission.Id, token);
                if (replayManifest is null)
                    return ManifestFailure("CAPTURE_MANIFEST_STATE_CONFLICT", "The original frozen manifest could not be found.");

                var replayJobId = await FindCaptureMappingJob(replayManifest).AsNoTracking()
                    .Select(value => (Guid?)value.Id).SingleOrDefaultAsync(token);
                return BuildFrozenManifestResult(order, attempt, replayManifest, replayJobId);
            }
            if (
                order.Status != SurveyOrderStatus.AwaitingCaptureUpload ||
                order.Version != request.ExpectedOrderVersion ||
                attempt.Status != CaptureAttemptStatus.AwaitingUpload ||
                attempt.Version != request.ExpectedAttemptVersion)
            {
                return ManifestFailure(
                    "CAPTURE_UPLOAD_VERSION_CONFLICT",
                    "The V3 capture graph is missing or stale.");
            }

            var existing = await db.Set<CaptureInputManifest>()
                .Where(value =>
                    value.CaptureAttemptId == attempt.Id &&
                    value.Status != CaptureInputManifestStatus.Superseded &&
                    value.Status != CaptureInputManifestStatus.Rejected)
                .OrderByDescending(value => value.CreatedAt)
                .FirstOrDefaultAsync(token);

            if (existing is not null)
            {
                return ManifestFailure(
                    "CAPTURE_MANIFEST_STATE_CONFLICT",
                    "An active manifest exists without a matching idempotent operation receipt.");
            }

            var filesQuery = db.Set<MissionMedia>()
                .AsNoTracking()
                .Where(value =>
                    value.MissionId == mission.Id &&
                    value.Media.StorageStatus == MediaStorageStatus.Active &&
                    value.Media.DeletedAt == null);
            if (mission.UploadRevisionId != Guid.Empty)
            {
                if (mission.Status != MissionStatus.Uploading)
                    return ManifestFailure("CAPTURE_MISSION_STATE_CONFLICT", "The replacement input must be uploaded before freezing.");

                var replacementSessions = db.Set<MediaUploadSession>().Where(session => session.MissionId == mission.Id &&
                    session.TenantId == order.TenantId && session.FarmId == order.FarmId &&
                    session.UploadRevisionId == mission.UploadRevisionId);
                if (await replacementSessions.AnyAsync(session => session.Status == MediaUploadSessionStatus.Verifying ||
                        (session.Status == MediaUploadSessionStatus.Pending && session.ExpiresAt > now), token))
                {
                    return ManifestFailure("CAPTURE_MEDIA_UPLOAD_IN_PROGRESS", "Finish the current uploads before freezing replacement input.");
                }
                filesQuery = filesQuery.Where(value => value.MediaRole == MissionMediaRole.RawImage &&
                    replacementSessions.Any(session => session.MediaAssetId == value.MediaId &&
                        session.Status == MediaUploadSessionStatus.Completed));
            }

            var files = await filesQuery
                .OrderBy(value => value.MediaId)
                .Select(value => new
                {
                    value.MediaId,
                    value.Media.Url,
                    value.Media.Checksum,
                    value.Media.FileSizeBytes,
                    Role = value.MediaRole.ToString()
                })
                .ToListAsync(token);
            if (files.Count == 0 || files.Any(value =>
                    string.IsNullOrWhiteSpace(value.Checksum) ||
                    value.Checksum.Length != 64 ||
                    value.FileSizeBytes is null or <= 0))
            {
                return ManifestFailure(
                    "CAPTURE_MEDIA_INCOMPLETE",
                    "Every frozen input file requires an immutable SHA-256 checksum and positive size.");
            }
            if (_objectStorage is not null)
            {
                foreach (var file in files)
                {
                    var stored = await _objectStorage.GetInfoAsync(file.Url, token);
                    if (stored is null || stored.FileSizeBytes != file.FileSizeBytes)
                    {
                        return ManifestFailure(
                            "CAPTURE_MEDIA_OBJECT_MISMATCH",
                            "Every frozen media record must resolve to an object with the same immutable size.");
                    }
                }
            }

            var telemetry = await ReadValidCaptureTelemetryAsync(order, mission, token);
            if (telemetry is null)
            {
                return ManifestFailure("CAPTURE_TELEMETRY_REQUIRED",
                    "Import valid Blackbox telemetry for this mission before freezing the input manifest.");
            }

            var telemetryEvidence = FormatTelemetryEvidence(telemetry);
            var canonical = string.Join(
                '\n',
                files.Select(value =>
                    $"{value.MediaId:D}|{value.Checksum!.ToLowerInvariant()}|{value.FileSizeBytes}|{value.Role}")
                    .Prepend(telemetryEvidence)
                    .Prepend($"attempt-version={attempt.Version}"));
            var fingerprint = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
            var manifest = CaptureInputManifest.Create(
                Guid.CreateVersion7(),
                order.TenantId,
                order.FarmId,
                order.Id,
                attempt.Id,
                mission.Id,
                fingerprint,
                files.Count,
                files.Sum(value => value.FileSizeBytes!.Value),
                now,
                telemetryEvidence);
            var job = AiProcessingJob.QueueCaptureMapping(mission.Id, manifest.Id, manifest.ManifestFingerprint, now);
            db.Set<CaptureInputManifest>().Add(manifest);
            db.Set<AiProcessingJob>().Add(job);

            var manifestFiles = files.Select(value => CaptureInputManifestFile.Create(
                manifest.Id,
                value.MediaId,
                value.Checksum!.ToLowerInvariant(),
                value.FileSizeBytes!.Value,
                value.Role,
                now)).ToList();
            db.Set<CaptureInputManifestFile>().AddRange(manifestFiles);

            attempt.BindMission(mission.Id);
            attempt.MarkUploaded(
                new CaptureUploadManifestEvidence(
                    order.TenantId,
                    order.FarmId,
                    order.Id,
                    attempt.Id,
                    mission.Id,
                    manifest.Id.ToString("D"),
                    manifest.ManifestFingerprint),
                now,
                "V3_CAPTURE_COORDINATOR",
                "Immutable capture input manifest frozen",
                request.ActorId,
                request.CorrelationId,
                request.OperationId,
                request.PayloadFingerprint,
                request.ExpectedAttemptVersion);
            order.TransitionToAwaitingInputReviewV3(now);
            AddCaptureEvidence(order, request.ActorId, request.CorrelationId,
                "CAPTURE_INPUT_MANIFEST_FROZEN", now, attempt.Id, mission.Id);
            await db.SaveChangesAsync(token);
            return BuildFrozenManifestResult(order, attempt, manifest, job.Id);
        },
        ManifestException,
        cancellationToken);

    private IQueryable<AiProcessingJob> FindCaptureMappingJob(CaptureInputManifest manifest) =>
        db.Set<AiProcessingJob>().Where(job => job.ClientOperationId == manifest.Id &&
            job.MissionId == manifest.MissionId && job.JobType == AiJobType.Mapping);

    private async Task<MissionTelemetryImport?> ReadValidCaptureTelemetryAsync(
        SurveyOrder order,
        DroneMission mission,
        CancellationToken token)
    {
        var telemetry = await db.Set<MissionTelemetryImport>().AsNoTracking()
            .SingleOrDefaultAsync(value => value.MissionId == mission.Id &&
                value.TenantId == order.TenantId && value.FarmId == order.FarmId &&
                value.UploadRevisionId == mission.UploadRevisionId, token);
        if (telemetry is null || telemetry.PointCount < 2 ||
            telemetry.PayloadChecksum is not { Length: 64 } checksum || !checksum.All(Uri.IsHexDigit) ||
            telemetry.SelectedSegmentJson?.RootElement is not { ValueKind: JsonValueKind.Object } segment ||
            !segment.TryGetProperty("points", out var points) ||
            points.ValueKind != JsonValueKind.Array || points.GetArrayLength() != telemetry.PointCount ||
            mission.StartedAt is null || mission.EndedAt is null ||
            telemetry.FirstRecordedAt >= telemetry.LastRecordedAt)
        {
            return null;
        }

        var persisted = await db.Set<MissionTelemetryPoint>().AsNoTracking()
            .Where(point => point.MissionId == mission.Id)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Count = group.Count(),
                First = group.Min(point => point.RecordedAt),
                Last = group.Max(point => point.RecordedAt)
            })
            .SingleOrDefaultAsync(token);
        return persisted?.Count == telemetry.PointCount && persisted.First == telemetry.FirstRecordedAt &&
            persisted.Last == telemetry.LastRecordedAt ? telemetry : null;
    }

    private static string FormatTelemetryEvidence(MissionTelemetryImport telemetry) =>
        $"telemetry={telemetry.Id:D}|operation={telemetry.OperationId:D}|sha256={telemetry.PayloadChecksum}|points={telemetry.PointCount}";

    private static FreezeCaptureInputManifestResult BuildFrozenManifestResult(
        SurveyOrder order, CaptureAttempt attempt, CaptureInputManifest manifest, Guid? jobId) =>
        new(true, attempt.Id, manifest.Id, manifest.ManifestFingerprint, manifest.TotalFileCount,
            manifest.TotalSizeBytes, attempt.Status.ToString(), attempt.Version,
            ProcessingJobId: jobId, OrderVersion: order.Version);

    public Task<CreateQualityReflightResult> CreateQualityReflightAsync(
        CreateQualityReflightRequest request,
        CancellationToken cancellationToken = default) => ExecuteAsync(
        async token =>
        {
            if (!await LockOrderAsync(request.OrderId, token))
            {
                return ReflightFailure("ORDER_NOT_FOUND", "Survey Order was not found.");
            }
            var now = clock.GetUtcNow();
            var order = await db.Set<SurveyOrder>().SingleAsync(
                value => value.Id == request.OrderId, token);
            if (!await IsAssignedManagerAsync(
                    order.TenantId,
                    order.FarmId,
                    request.ActorId,
                    token))
            {
                return ReflightFailure("MANAGER_FORBIDDEN", "The actor is not the active assigned Manager.");
            }

            var replay = await db.Set<DroneMission>()
                .AsNoTracking()
                .Where(value =>
                    value.SurveyOrderId == request.OrderId &&
                    value.PreparationOperationId == request.OperationId &&
                    value.CaptureAttemptId != null)
                .Select(value => new { value.Id, AttemptId = value.CaptureAttemptId!.Value })
                .SingleOrDefaultAsync(token);
            if (replay is not null)
            {
                var compatible = await IsOperationReplayCompatibleAsync(
                    replay.AttemptId,
                    request.OperationId,
                    request.PayloadFingerprint,
                    token);
                if (!compatible)
                {
                    return ReflightFailure("OPERATION_CONFLICT", "The operation identifier was already used with another payload.");
                }
                var replayAttempt = await db.Set<CaptureAttempt>()
                    .AsNoTracking()
                    .SingleAsync(value => value.Id == replay.AttemptId, token);
                return new CreateQualityReflightResult(
                    true,
                    replayAttempt.Id,
                    replayAttempt.AttemptIndex,
                    replay.Id,
                    replayAttempt.Status.ToString(),
                    replayAttempt.Version);
            }

            var parent = await db.Set<CaptureAttempt>()
                .Include(value => value.History)
                .SingleOrDefaultAsync(value =>
                    value.Id == request.ParentAttemptId &&
                    value.OrderId == request.OrderId,
                    token);
            if (parent is null ||
                order.Scope != ContractScope.V3 ||
                order.Status != SurveyOrderStatus.AwaitingReflightDecision ||
                order.Version != request.ExpectedOrderVersion ||
                parent.Version != request.ExpectedParentAttemptVersion)
            {
                return ReflightFailure("REFLIGHT_VERSION_CONFLICT", "Parent Attempt or Order state is stale.");
            }
            if (request.ProposedStartAt <= now ||
                request.ProposedEndAt <= request.ProposedStartAt)
            {
                return ReflightFailure("APPOINTMENT_WINDOW_INVALID", "A future Capture appointment window is required.");
            }

            var obligation = order.DepositObligationId.HasValue
                ? await db.Set<SurveyDepositObligation>().SingleOrDefaultAsync(value =>
                    value.Id == order.DepositObligationId.Value &&
                    value.SurveyOrderId == order.Id,
                    token)
                : null;
            if (obligation?.IsSatisfied != true)
            {
                return ReflightFailure("DEPOSIT_NOT_SATISFIED", "Quality Re-flight reuses the original satisfied deposit obligation.");
            }

            await LockDroneAsync(request.DroneId, token);
            var drone = await db.Set<Drone>().SingleOrDefaultAsync(
                value => value.Id == request.DroneId,
                token);
            var commercial = await db.Set<SurveyOrderCommercialSnapshot>()
                .AsNoTracking()
                .SingleOrDefaultAsync(value => value.SurveyOrderId == order.Id, token);
            var isDiseaseCapture = parent.MappingSourceAttemptId.HasValue;
            var parentMission = isDiseaseCapture ? await db.Set<DroneMission>().AsNoTracking().SingleOrDefaultAsync(
                value => value.CaptureAttemptId == parent.Id && value.SurveyOrderId == order.Id &&
                    value.TenantId == order.TenantId && value.FarmId == order.FarmId, token) : null;
            if (isDiseaseCapture && parentMission is null)
                return ReflightFailure("CAPTURE_GRAPH_NOT_FOUND", "The disease capture mission could not be found.");

            if (drone is null ||
                !drone.IsOperationalFor(request.ProposedStartAt, request.ProposedEndAt) ||
                commercial is null ||
                (!isDiseaseCapture && commercial.MapsPoles && !drone.SupportsPurpose(MissionPurpose.BaselineMapping)) ||
                (commercial.ScreensDisease && !drone.SupportsPurpose(MissionPurpose.PlantHealth)) ||
                (isDiseaseCapture && await HasOverlappingDroneMissionAsync(
                    request.DroneId, request.ProposedStartAt, request.ProposedEndAt, null, token)))
            {
                return ReflightFailure("DRONE_NOT_READY", "The selected drone is not operational or lacks the pinned service capabilities.");
            }

            var managerProfileId = await ReadAssignedManagerProfileIdAsync(
                order.TenantId,
                order.FarmId,
                request.ActorId,
                token);
            if (!managerProfileId.HasValue)
            {
                return ReflightFailure("MANAGER_FORBIDDEN", "The assigned Manager profile was not found.");
            }

            var appointmentPurpose = isDiseaseCapture
                ? SurveyAppointmentPurpose.DiseaseCapture
                : SurveyAppointmentPurpose.Capture;
            var latestAppointment = await db.Set<SurveyAppointment>()
                .Where(value =>
                    value.SurveyOrderId == order.Id &&
                    value.Purpose == appointmentPurpose)
                .OrderByDescending(value => value.RevisionNumber)
                .ThenByDescending(value => value.CreatedAt)
                .FirstOrDefaultAsync(token);
            if (latestAppointment?.Status is SurveyAppointmentStatus.Proposed or SurveyAppointmentStatus.Confirmed)
            {
                latestAppointment.Cancel(now);
            }
            else if (latestAppointment?.Status == SurveyAppointmentStatus.RescheduleRequested)
            {
                latestAppointment.Supersede(now);
            }
            if (latestAppointment is not null)
                await db.SaveChangesAsync(token);

            var appointment = SurveyAppointment.Create(
                order.Id,
                appointmentPurpose,
                (latestAppointment?.RevisionNumber ?? 0) + 1,
                latestAppointment?.Id,
                request.ActorId,
                request.ProposedStartAt,
                request.ProposedEndAt,
                now);
            if (isDiseaseCapture)
                appointment.ConfirmDiseaseCaptureByManager(request.ActorId, now);

            var successorId = Guid.CreateVersion7();
            using var flightParameters = JsonSerializer.SerializeToDocument(new
            {
                contract = "SURVEY_CAPTURE_V3_REFLIGHT",
                orderId = order.Id,
                captureAttemptId = successorId,
                parentAttemptId = parent.Id,
                mappingSourceAttemptId = parent.MappingSourceAttemptId,
                MapsPoles = commercial.MapsPoles && !isDiseaseCapture,
                commercial.ScreensDisease,
                commercial.PolicyFingerprint
            });
            var mission = DroneMission.CreateOrderBound(
                order.Id,
                order.TenantId,
                order.FarmId,
                parentMission?.ScopeZoneIds ?? [],
                parentMission?.FarmBoundaryVersionId ?? order.FarmBoundaryVersionId,
                drone.Id,
                request.ActorId,
                $"V3R{parent.AttemptIndex + 1}-{order.Id:N}",
                MissionPurpose.Capture,
                request.OperationId,
                parentMission?.SourceMapVersionId ?? order.FarmBaseMapVersionId,
                requiresBaselineCompletion: false,
                flightParameters,
                request.ActorId,
                now,
                parentMission?.ScopeType ?? MissionScopeType.FarmWide,
                parentMission?.ScopeSnapshot,
                parentMission?.ScopeFingerprint ?? "FARM_WIDE");
            mission.Schedule(request.ProposedStartAt, request.ProposedEndAt, now);
            var successor = CaptureAttempt.CreateSuccessor(
                successorId,
                parent,
                mission.Id,
                request.ActorId,
                request.Reason,
                request.TechnicalQualityEvidence,
                request.ActorId,
                "V3_CAPTURE_COORDINATOR",
                request.CorrelationId,
                request.OperationId,
                request.PayloadFingerprint,
                now,
                parentMission?.Id);
            mission.BindCaptureAttempt(successor.Id);
            if (isDiseaseCapture)
                order.TransitionToReadyForCaptureV3(now);
            else
                order.TransitionToAwaitingAppointmentV3(now);
            db.Set<SurveyAppointment>().Add(appointment);
            db.Set<DroneMission>().Add(mission);
            db.Set<CaptureAttempt>().Add(successor);
            AddCaptureEvidence(order, request.ActorId, request.CorrelationId,
                "QUALITY_REFLIGHT_PREPARED", now, successor.Id, mission.Id);
            await db.SaveChangesAsync(token);
            return new CreateQualityReflightResult(
                true,
                successor.Id,
                successor.AttemptIndex,
                mission.Id,
                successor.Status.ToString(),
                successor.Version);
        },
        ReflightException,
        cancellationToken);

    public Task<CancelCaptureOrderResult> CancelOrderAsync(
        CancelCaptureOrderRequest request,
        CancellationToken cancellationToken = default) => ExecuteAsync(
        async token =>
        {
            var scope = await db.Set<SurveyOrder>()
                .AsNoTracking()
                .Where(value => value.Id == request.OrderId)
                .Select(value => new { value.Id, value.TenantId })
                .SingleOrDefaultAsync(token);
            if (scope is null)
            {
                return CancelFailure(request.OrderId, "ORDER_NOT_FOUND", "Survey Order was not found.");
            }

            var decisionKind = request.DecisionKind switch
            {
                CaptureCancellationDecisionKind.CustomerBeforeCapture => CancellationDecisionKind.CustomerBeforeCapture,
                CaptureCancellationDecisionKind.CustomerAfterAcceptableCapture => CancellationDecisionKind.CustomerAfterAcceptableCapture,
                CaptureCancellationDecisionKind.OperationalPostponement => CancellationDecisionKind.OperationalPostponement,
                CaptureCancellationDecisionKind.ServiceFailure => CancellationDecisionKind.ServiceFailure,
                _ => throw new InvalidOperationException("Unsupported cancellation decision kind.")
            };

            // ADR 0020 Rank 1: Authorize caller BEFORE acquiring Order Rank 2 lock or reading funding facts
            var ownerDecision = decisionKind == CancellationDecisionKind.CustomerBeforeCapture;
            if (ownerDecision)
            {
                if (request.ActorId != request.RequesterId ||
                    !await HasOwnerAccessAsync(scope.TenantId, request.ActorId, token))
                {
                    return CancelFailure(scope.Id, "OWNER_FORBIDDEN", "Before-capture cancellation requires the active Tenant Owner.");
                }
            }
            else if (!await IsSystemAdminAsync(request.ActorId, token))
            {
                return CancelFailure(scope.Id, "ADMIN_DECISION_REQUIRED", "This cancellation decision requires a System Admin.");
            }

            // ADR 0020 Rank 2: Lock Order FOR UPDATE and reverify scope
            if (!await LockOrderAsync(request.OrderId, token))
            {
                return CancelFailure(request.OrderId, "ORDER_NOT_FOUND", "Survey Order was not found.");
            }

            var now = clock.GetUtcNow();
            var order = await db.Set<SurveyOrder>().SingleAsync(
                value => value.Id == request.OrderId, token);
            if (order.TenantId != scope.TenantId)
            {
                return CancelFailure(order.Id, "OWNER_FORBIDDEN", "Order tenant context changed during cancellation authorization.");
            }

            // ADR 0020 Rank 3: Idempotent replay check
            var replay = await db.Set<SurveyCancellationDecision>()
                .AsNoTracking()
                .SingleOrDefaultAsync(value =>
                    value.SurveyOrderId == request.OrderId &&
                    value.OperationId == request.OperationId,
                    token);
            if (replay is not null)
            {
                if (!string.Equals(replay.PayloadFingerprint, request.PayloadFingerprint, StringComparison.Ordinal))
                {
                    return CancelFailure(request.OrderId, "OPERATION_CONFLICT", "The operation identifier was already used with another payload.");
                }

                var replayRefundId = await db.Set<SurveyRefundObligation>()
                    .AsNoTracking()
                    .Where(value => value.OriginDecisionId == replay.Id)
                    .Select(value => (Guid?)value.Id)
                    .SingleOrDefaultAsync(token);
                return new CancelCaptureOrderResult(
                    true,
                    request.OrderId,
                    SurveyOrderStatus.Cancelled.ToString(),
                    replay.RefundAmount,
                    replay.RetainedAmount,
                    replay.Id,
                    replayRefundId);
            }

            if (order.Scope != ContractScope.V3 ||
                order.Version != request.ExpectedOrderVersion ||
                order.Status == SurveyOrderStatus.Cancelled)
            {
                return CancelFailure(order.Id, "CANCELLATION_VERSION_CONFLICT", "The V3 Order is already terminal or its expected version is stale.");
            }
            if (order.Status == SurveyOrderStatus.Capturing)
            {
                return CancelFailure(order.Id, "CAPTURE_IN_FLIGHT", "An in-flight capture must end safely before cancellation can become terminal.");
            }

            if (ownerDecision)
            {
                if (order.Status is not (
                    SurveyOrderStatus.AwaitingAppointment or
                    SurveyOrderStatus.AwaitingDeposit or
                    SurveyOrderStatus.ReadyForCapture))
                {
                    return CancelFailure(order.Id, "ADMIN_DECISION_REQUIRED", "Cancellation after capture execution requires an evidence-based System Admin decision.");
                }
            }

            var snapshot = await db.Set<SurveyOrderCommercialSnapshot>()
                .AsNoTracking()
                .SingleOrDefaultAsync(value => value.SurveyOrderId == order.Id, token);
            var policy = snapshot is null
                ? null
                : await db.Set<CancellationPolicy>()
                    .AsNoTracking()
                    .SingleOrDefaultAsync(value =>
                        value.Id == snapshot.CancellationPolicyId &&
                        value.VersionNumber == snapshot.CancellationPolicyVersionNumber,
                        token);
            if (snapshot is null || policy is null)
            {
                return CancelFailure(order.Id, "PINNED_POLICY_NOT_FOUND", "The exact pinned cancellation policy is unavailable.");
            }

            CaptureInputApproval? inputApproval = null;
            if (request.InputApprovalEvidenceId.HasValue)
            {
                inputApproval = await db.Set<CaptureInputApproval>()
                    .AsNoTracking()
                    .SingleOrDefaultAsync(value =>
                        value.Id == request.InputApprovalEvidenceId.Value &&
                        value.SurveyOrderId == order.Id,
                        token);
                if (inputApproval is null)
                {
                    return CancelFailure(order.Id, "INPUT_APPROVAL_NOT_FOUND", "Acceptable-capture evidence does not belong to this Order.");
                }
            }

            // ADR 0020 Rank 4: Lock active capture appointments before Rank 5 funding/deposit locks
            var activeAppointments = db.Database.IsRelational()
                ? await db.Set<SurveyAppointment>()
                    .FromSqlInterpolated($"""
                        SELECT appointment.*, appointment.xmin
                        FROM survey.survey_appointments AS appointment
                        WHERE appointment.survey_order_id = {order.Id}
                          AND appointment.purpose IN ('CAPTURE', 'DISEASE_CAPTURE')
                          AND appointment.status IN (
                            'PROPOSED'::system.survey_appointment_status,
                            'CONFIRMED'::system.survey_appointment_status,
                            'RESCHEDULE_REQUESTED'::system.survey_appointment_status)
                        ORDER BY appointment.id
                        FOR UPDATE
                        """)
                    .ToListAsync(token)
                : await db.Set<SurveyAppointment>()
                    .Where(value =>
                        value.SurveyOrderId == order.Id &&
                        (value.Purpose == SurveyAppointmentPurpose.Capture || value.Purpose == SurveyAppointmentPurpose.DiseaseCapture) &&
                        (value.Status == SurveyAppointmentStatus.Proposed ||
                         value.Status == SurveyAppointmentStatus.Confirmed ||
                         value.Status == SurveyAppointmentStatus.RescheduleRequested))
                    .OrderBy(value => value.Id)
                    .ToListAsync(token);

            // ADR 0020 Rank 5: Lock DepositObligation (FOR UPDATE) + shared funding/reversal/refund ledger
            SurveyDepositObligation? deposit = null;
            if (order.DepositObligationId.HasValue)
            {
                deposit = db.Database.IsRelational()
                    ? await db.Set<SurveyDepositObligation>()
                        .FromSqlInterpolated($"SELECT d.* FROM survey.survey_deposit_obligations AS d WHERE d.id = {order.DepositObligationId.Value} AND d.survey_order_id = {order.Id} FOR UPDATE")
                        .SingleOrDefaultAsync(token)
                    : await db.Set<SurveyDepositObligation>()
                        .SingleOrDefaultAsync(value =>
                            value.Id == order.DepositObligationId.Value &&
                            value.SurveyOrderId == order.Id,
                            token);
                if (deposit is null)
                {
                    return CancelFailure(order.Id, "REFUND_RECONCILIATION_HOLD", "Pinned deposit obligation could not be found.");
                }
            }

            var fundingResult = await SurveyInvoiceFunding.ReadAsync(
                db,
                order,
                deposit,
                currentInvoiceRevisionId: order.CurrentInvoiceRevisionId,
                lockRows: true,
                token,
                allowHistoricalUnlinkedRefunds: true);
            if (fundingResult.IsFailure)
            {
                return CancelFailure(order.Id, "REFUND_RECONCILIATION_HOLD", fundingResult.Error.Description);
            }

            var funding = fundingResult.Value;
            // Historical unlinked refunds still consume the Order-level cap.
            if (funding.ExcludedReceiptIds.Count > 0)
            {
                return CancelFailure(
                    order.Id,
                    "REFUND_RECONCILIATION_HOLD",
                    "Order has funding receipts requiring Admin reconciliation before cancellation.");
            }

            var available = funding.AvailableRefundCap;
            var retained = request.RequestedRetentionAmount;
            if (retained < 0m || decimal.Truncate(retained) != retained)
            {
                return CancelFailure(order.Id, "INVALID_RETENTION_AMOUNT", "Requested retention amount must be a non-negative whole VND amount.");
            }

            var maxRetention = Math.Min(snapshot.DepositAmount, available);
            if (retained > maxRetention)
            {
                return CancelFailure(
                    order.Id,
                    CancellationDomainErrorCodes.RetentionExceedsAllowedCap,
                    $"Requested retention amount ({retained}) exceeds allowed cap min(D={snapshot.DepositAmount}, Available={available}) = {maxRetention}.");
            }

            var refund = available - retained;
            var terms = PinnedCancellationPolicyTerms.Create(
                policy.Id,
                policy.VersionNumber,
                policy.RefundableBeforeCapture,
                policy.RetainDepositAfterCapture,
                policy.AllowedFreeNoShowReschedules,
                policy.OperationalPostponementRefundable,
                policy.ServiceFailureRefundable);

            // SurveyCancellationDecision.Create and ck_survey_cancellation_decisions_arithmetic compute
            // unreserved = confirmedReceiptsAmount - completedRefundsAmount - activeRefundReservationsAmount.
            // Pass netReceiptsAfterReversals (gross - cashReversals) as confirmedReceiptsAmount so completed
            // refunds and active reservations are never double-subtracted, and persist the full reversal basis
            // in the ORDER_CANCELLED AuditLog metadata.
            var netReceiptsAfterReversals = funding.GrossConfirmedReceipts - funding.TotalCashReversals;
            var decision = SurveyCancellationDecision.Create(
                Guid.CreateVersion7(),
                order.Id,
                order.Version,
                request.RequesterId,
                request.ActorId,
                terms,
                decisionKind,
                request.ReasonCode,
                request.ReasonDescription,
                netReceiptsAfterReversals,
                funding.TotalCompletedRefunds,
                funding.TotalActiveReservations,
                snapshot.DepositAmount,
                retained,
                refund,
                snapshot.Currency,
                request.PayloadFingerprint,
                now,
                inputApproval?.Id,
                operationId: request.OperationId);

            SurveyRefundObligation? refundObligation = null;
            if (decision.RefundAmount > 0m)
            {
                refundObligation = SurveyRefundObligation.Create(
                    Guid.CreateVersion7(),
                    order.Id,
                    order.TenantId,
                    decisionKind == CancellationDecisionKind.ServiceFailure
                        ? SurveyRefundOriginKind.ServiceFailure
                        : SurveyRefundOriginKind.CancellationReturn,
                    decision.Id,
                    decision.RefundAmount,
                    decision.Currency,
                    now);
                db.Set<SurveyRefundObligation>().Add(refundObligation);
            }
            if (decision.RetainedAmount > 0m)
            {
                db.Set<CancellationRetention>().Add(CancellationRetention.Create(
                    Guid.CreateVersion7(),
                    order.Id,
                    order.TenantId,
                    decision.Id,
                    decision.RetainedAmount,
                    decision.Currency,
                    request.ReasonDescription,
                    now));
            }

            // ADR 0020 Rank 7 & Rank 8: CaptureAttempt -> DroneMission
            var activeAttempt = await db.Set<CaptureAttempt>()
                .Include(value => value.History)
                .SingleOrDefaultAsync(value =>
                    value.OrderId == order.Id &&
                    (value.Status == CaptureAttemptStatus.Planned ||
                     value.Status == CaptureAttemptStatus.PreflightReady ||
                     value.Status == CaptureAttemptStatus.InFlight ||
                     value.Status == CaptureAttemptStatus.AwaitingUpload ||
                     value.Status == CaptureAttemptStatus.Uploaded ||
                     value.Status == CaptureAttemptStatus.Processing),
                    token);
            if (activeAttempt?.Status == CaptureAttemptStatus.InFlight)
            {
                return CancelFailure(order.Id, "CAPTURE_IN_FLIGHT", "An in-flight capture must end safely before cancellation can become terminal.");
            }
            if (activeAttempt?.Status is CaptureAttemptStatus.Planned or CaptureAttemptStatus.PreflightReady)
            {
                var activeMission = await db.Set<DroneMission>()
                    .SingleAsync(value => value.CaptureAttemptId == activeAttempt.Id, token);
                activeAttempt.BindMission(activeMission.Id);
                activeAttempt.Cancel(
                    request.ReasonDescription,
                    request.InputApprovalEvidenceId?.ToString("D"),
                    request.ActorId,
                    "V3_CAPTURE_CANCELLATION",
                    request.CorrelationId,
                    request.OperationId,
                    request.PayloadFingerprint,
                    now,
                    activeAttempt.Version);
                activeMission.Cancel(now);
            }

            deposit?.Cancel(now);
            foreach (var appointment in activeAppointments)
            {
                if (appointment.Status == SurveyAppointmentStatus.RescheduleRequested)
                {
                    appointment.Supersede(now);
                }
                else
                {
                    appointment.Cancel(now);
                }
            }

            order.TransitionToCancelledV3(now);
            db.Set<SurveyCancellationDecision>().Add(decision);
            AddCaptureEvidence(order, request.ActorId, request.CorrelationId,
                "ORDER_CANCELLED", now, activeAttempt?.Id, null,
                decision.Id, refundObligation?.Id,
                fundingBasis: new
                {
                    grossConfirmedReceiptsAmount = funding.GrossConfirmedReceipts,
                    cashReversalsAmount = funding.TotalCashReversals,
                    netConfirmedReceiptsAfterReversals = netReceiptsAfterReversals,
                    completedRefundsAmount = funding.TotalCompletedRefunds,
                    activeRefundReservationsAmount = funding.TotalActiveReservations,
                    availableRefundableAmount = available,
                    pinnedDepositAmount = snapshot.DepositAmount,
                    retainedAmount = decision.RetainedAmount,
                    refundAmount = decision.RefundAmount,
                    hasUnlinkedHistoricalRefunds = funding.HasUnlinkedRefundObligations,
                    hasOpenReconciliation = funding.HasOpenReconciliation,
                    reconciliationReasons = funding.ReconciliationReasons,
                    excludedSettlementReceiptIds = funding.ExcludedReceiptIds,
                    fundingReceiptIds = funding.ReceiptIds,
                    fundingReversalIds = funding.ReversalIds,
                    existingRefundObligationIds = funding.ActiveOrCompletedRefundIds
                });
            await db.SaveChangesAsync(token);
            return new CancelCaptureOrderResult(
                true,
                order.Id,
                order.Status.ToString(),
                decision.RefundAmount,
                decision.RetainedAmount,
                decision.Id,
                refundObligation?.Id);
        },
        exception => CancelException(request.OrderId, exception),
        cancellationToken);

    public Task<RecordCaptureNoShowResult> RecordNoShowAsync(
        RecordCaptureNoShowRequest request,
        CancellationToken cancellationToken = default) => ExecuteAsync(
        async token =>
        {
            if (!await LockOrderAsync(request.OrderId, token))
            {
                return NoShowFailure(request, "ORDER_NOT_FOUND", "The Survey Order was not found.");
            }

            var replay = await db.Set<SurveyAppointmentNoShow>()
                .AsNoTracking()
                .SingleOrDefaultAsync(value => value.OperationId == request.OperationId, token);
            if (replay is not null)
            {
                if (replay.SurveyOrderId != request.OrderId ||
                    replay.SurveyAppointmentId != request.AppointmentId ||
                    !string.Equals(replay.PayloadFingerprint, request.PayloadFingerprint, StringComparison.Ordinal))
                {
                    return NoShowFailure(request, "OPERATION_CONFLICT", "The operation ID was already used with a different payload.");
                }

                var replayOrder = await db.Set<SurveyOrder>().AsNoTracking()
                    .SingleAsync(value => value.Id == replay.SurveyOrderId, token);
                var replayAppointment = await db.Set<SurveyAppointment>().AsNoTracking()
                    .SingleAsync(value => value.Id == replay.SurveyAppointmentId, token);
                return new RecordCaptureNoShowResult(
                    true,
                    replay.SurveyOrderId,
                    replay.SurveyAppointmentId,
                    replay.Id,
                    replay.OccurrenceIndex,
                    replay.Disposition.ToString(),
                    replayOrder.Status.ToString(),
                    replayAppointment.Status.ToString());
            }

            var order = await db.Set<SurveyOrder>()
                .SingleOrDefaultAsync(value => value.Id == request.OrderId, token);
            if (order is null ||
                order.Scope != ContractScope.V3 ||
                order.Status != SurveyOrderStatus.ReadyForCapture ||
                order.Version != request.ExpectedOrderVersion)
            {
                return NoShowFailure(request, "ORDER_NOT_READY", "The V3 Order is not at the expected ReadyForCapture version.");
            }
            if (!await IsAssignedManagerAsync(order.TenantId, order.FarmId, request.ActorId, token))
            {
                return NoShowFailure(request, "MANAGER_FORBIDDEN", "Only the active assigned Manager can record operational no-show evidence.");
            }

            if (await db.Set<CaptureAttempt>().AnyAsync(value =>
                    value.OrderId == order.Id && value.MappingSourceAttemptId != null, token))
                return NoShowFailure(request, "APPOINTMENT_VERSION_CONFLICT", "The original Capture appointment is complete. Reschedule the disease capture with the Manager appointment endpoint.");

            var appointment = await db.Set<SurveyAppointment>()
                .SingleOrDefaultAsync(value =>
                    value.Id == request.AppointmentId &&
                    value.SurveyOrderId == order.Id &&
                    value.Purpose == SurveyAppointmentPurpose.Capture,
                    token);
            if (appointment is null ||
                appointment.Status != SurveyAppointmentStatus.Confirmed ||
                appointment.Version != request.ExpectedAppointmentVersion)
            {
                return NoShowFailure(request, "APPOINTMENT_VERSION_CONFLICT", "The confirmed Capture appointment is missing or changed.");
            }
            var recordedAt = clock.GetUtcNow();
            if (request.OccurredAt.Offset != TimeSpan.Zero ||
                request.OccurredAt < appointment.ProposedStartAt ||
                request.OccurredAt > recordedAt)
            {
                return NoShowFailure(request, "NO_SHOW_TIME_INVALID", "No-show evidence can only be recorded in UTC after the appointment starts.");
            }

            var snapshot = await db.Set<SurveyOrderCommercialSnapshot>()
                .AsNoTracking()
                .SingleOrDefaultAsync(value => value.SurveyOrderId == order.Id, token);
            if (snapshot is null)
            {
                return NoShowFailure(request, "COMMERCIAL_SNAPSHOT_MISSING", "The pinned cancellation policy snapshot is required.");
            }

            var occurrenceIndex = await db.Set<SurveyAppointmentNoShow>()
                .CountAsync(value => value.SurveyOrderId == order.Id, token) + 1;
            var disposition = occurrenceIndex <= snapshot.AllowedFreeNoShowReschedules
                ? SurveyNoShowDisposition.FreeReschedule
                : SurveyNoShowDisposition.AdminReviewRequired;
            var noShow = SurveyAppointmentNoShow.Create(
                Guid.CreateVersion7(),
                order.Id,
                appointment.Id,
                occurrenceIndex,
                disposition,
                request.ActorId,
                request.Reason,
                request.EvidenceReference,
                request.OperationId,
                request.PayloadFingerprint,
                request.OccurredAt,
                recordedAt);

            appointment.RequestReschedule(
                disposition == SurveyNoShowDisposition.FreeReschedule
                    ? $"NO_SHOW_FREE_RESCHEDULE: {request.Reason}"
                    : $"NO_SHOW_ADMIN_REVIEW_REQUIRED: {request.Reason}",
                request.OccurredAt);
            order.TransitionToAwaitingAppointmentV3(request.OccurredAt);
            db.Set<SurveyAppointmentNoShow>().Add(noShow);
            AddCaptureEvidence(
                order,
                request.ActorId,
                request.CorrelationId,
                disposition == SurveyNoShowDisposition.FreeReschedule
                    ? "NO_SHOW_FREE_RESCHEDULE_RECORDED"
                    : "NO_SHOW_ADMIN_REVIEW_REQUIRED",
                request.OccurredAt);
            await db.SaveChangesAsync(token);

            return new RecordCaptureNoShowResult(
                true,
                order.Id,
                appointment.Id,
                noShow.Id,
                occurrenceIndex,
                disposition.ToString(),
                order.Status.ToString(),
                appointment.Status.ToString());
        },
        exception => NoShowFailure(request, "NO_SHOW_CONFLICT", exception.Message),
        cancellationToken);

    private static RecordCaptureNoShowResult NoShowFailure(
        RecordCaptureNoShowRequest request,
        string code,
        string message) => new(
        false,
        request.OrderId,
        request.AppointmentId,
        Guid.Empty,
        0,
        string.Empty,
        string.Empty,
        string.Empty,
        code,
        message);

    private void AddCaptureEvidence(
        SurveyOrder order,
        Guid actorId,
        Guid correlationId,
        string changeType,
        DateTimeOffset changedAt,
        Guid? attemptId = null,
        Guid? missionId = null,
        Guid? cancellationDecisionId = null,
        Guid? refundObligationId = null,
        object? fundingBasis = null,
        JsonDocument? oldData = null)
    {
        using var newData = JsonSerializer.SerializeToDocument(new
        {
            orderStatus = order.Status.ToString(),
            attemptId,
            missionId,
            cancellationDecisionId,
            refundObligationId,
            fundingBasis
        });
        db.Set<AuditLog>().Add(AuditLog.ForUserAction(
            order.TenantId,
            order.FarmId,
            actorId,
            correlationId,
            nameof(SurveyOrder),
            order.Id,
            changeType,
            oldData,
            newData,
            changedAt));

        var payload = new SurveyCaptureStateChangedV1(
            order.Id,
            order.TenantId,
            order.FarmId,
            order.Status.ToString(),
            changeType,
            attemptId,
            missionId,
            cancellationDecisionId,
            refundObligationId,
            changedAt);
        var envelope = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.SurveyCaptureStateChangedV1,
            Guid.CreateVersion7(),
            correlationId,
            order.TenantId,
            actorId,
            changedAt,
            payload);
        db.Set<OutboxMessage>().Add(outboxFactory.Create(
            envelope,
            IntegrationEventTypes.SurveyCaptureStateChangedV1,
            order.Id.ToString("D")));
    }

    private async Task<TResult> ExecuteAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        Func<Exception, TResult> exceptionMapper,
        CancellationToken cancellationToken,
        IsolationLevel isolationLevel = IsolationLevel.Serializable)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            isolationLevel,
            cancellationToken);
        try
        {
            var result = await operation(cancellationToken);
            await _probe.HitAsync(SurveyCaptureCheckpoint.AfterSaveBeforeCommit, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception exception) when (exception is
            InvalidOperationException or
            ArgumentException or
            SurveyDomainException or
            DbUpdateException or
            PostgresException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            return exceptionMapper(exception);
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    private async Task<bool> LockOrderAsync(Guid orderId, CancellationToken token)
    {
        await _probe.HitAsync(SurveyCaptureCheckpoint.BeforeOrderLock, token);
        return await LockRowAsync("SELECT id FROM survey.survey_orders WHERE id = @id FOR UPDATE", orderId, token);
    }

    private Task<bool> LockDroneAsync(Guid droneId, CancellationToken token) =>
        LockRowAsync("SELECT id FROM mission.drones WHERE id = @id FOR UPDATE", droneId, token);

    private async Task<bool> LockRowAsync(string sql, Guid id, CancellationToken token)
    {
        await using var command = CreateCommand(sql);
        command.Parameters.Add(new NpgsqlParameter("id", id));
        return await command.ExecuteScalarAsync(token) is Guid;
    }

    private async Task<bool> IsOperationReplayCompatibleAsync(
        Guid attemptId,
        Guid operationId,
        string payloadFingerprint,
        CancellationToken token)
    {
        var storedFingerprint = await ReadOperationFingerprintAsync(
            attemptId,
            operationId,
            token);
        return storedFingerprint is not null &&
            string.Equals(storedFingerprint, payloadFingerprint, StringComparison.Ordinal);
    }

    private Task<string?> ReadOperationFingerprintAsync(
        Guid attemptId,
        Guid operationId,
        CancellationToken token) => db.Set<CaptureAttemptHistory>()
            .AsNoTracking()
            .Where(value =>
                value.CaptureAttemptId == attemptId &&
                value.OperationId == operationId)
            .Select(value => value.PayloadFingerprint)
            .SingleOrDefaultAsync(token);

    private DbCommand CreateCommand(string sql)
    {
        var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        return command;
    }

    private async Task<bool> IsAssignedManagerAsync(
        Guid tenantId,
        Guid farmId,
        Guid actorId,
        CancellationToken token)
    {
        if (db.Database.IsRelational())
        {
            await _probe.HitAsync(SurveyCaptureCheckpoint.BeforeAssignmentLock, token);
            await using var command = CreateCommand("""
                SELECT assignment.id
                FROM identity.farm_manager_assignments AS assignment
                INNER JOIN identity.system_manager_profiles AS profile
                    ON profile.id = assignment.system_manager_profile_id
                WHERE assignment.tenant_id = @tenant_id
                  AND assignment.farm_id = @farm_id
                  AND assignment.ended_at IS NULL
                  AND profile.user_id = @actor_id
                  AND profile.status = 'ACTIVE'::system.system_manager_profile_status
                FOR SHARE OF assignment
                """);
            command.Parameters.Add(new NpgsqlParameter("tenant_id", tenantId));
            command.Parameters.Add(new NpgsqlParameter("farm_id", farmId));
            command.Parameters.Add(new NpgsqlParameter("actor_id", actorId));
            return await command.ExecuteScalarAsync(token) is Guid;
        }

        return await db.Set<FarmManagerAssignment>()
            .AsNoTracking()
            .AnyAsync(value =>
                value.TenantId == tenantId &&
                value.FarmId == farmId &&
                value.EndedAt == null &&
                value.SystemManagerProfile.UserId == actorId &&
                value.SystemManagerProfile.Status == SystemManagerProfileStatus.Active,
                token);
    }

    private Task<Guid?> ReadAssignedManagerProfileIdAsync(
        Guid tenantId,
        Guid farmId,
        Guid actorId,
        CancellationToken token) => db.Set<FarmManagerAssignment>()
        .AsNoTracking()
        .Where(value =>
            value.TenantId == tenantId &&
            value.FarmId == farmId &&
            value.EndedAt == null &&
            value.SystemManagerProfile.UserId == actorId &&
            value.SystemManagerProfile.Status == SystemManagerProfileStatus.Active)
        .Select(value => (Guid?)value.SystemManagerProfileId)
        .SingleOrDefaultAsync(token);

    private async Task<bool> HasOwnerAccessAsync(
        Guid tenantId,
        Guid actorId,
        CancellationToken token)
    {
        await using var command = CreateCommand(
            """
            SELECT membership.id
            FROM identity.tenant_memberships AS membership
            JOIN identity.users AS app_user ON app_user.id = membership.user_id
            WHERE membership.tenant_id = @tenant_id
              AND membership.user_id = @actor_id
              AND membership.role = 'OWNER'::system.tenant_member_role
              AND membership.status = 'ACTIVE'::system.general_status
              AND app_user.status = 'ACTIVE'::system.user_status
            FOR SHARE OF membership, app_user
            """);
        command.Parameters.Add(new NpgsqlParameter("tenant_id", tenantId));
        command.Parameters.Add(new NpgsqlParameter("actor_id", actorId));
        return await command.ExecuteScalarAsync(token) is Guid;
    }

    private async Task<bool> IsSystemAdminAsync(
        Guid actorId,
        CancellationToken token)
    {
        await using var command = CreateCommand(
            """
            SELECT user_role.user_id
            FROM identity.user_roles AS user_role
            JOIN identity.roles AS role ON role.id = user_role.role_id
            JOIN identity.users AS app_user ON app_user.id = user_role.user_id
            WHERE user_role.user_id = @actor_id
              AND role.code = 'SYSTEM_ADMIN'
              AND app_user.status = 'ACTIVE'::system.user_status
            FOR SHARE OF user_role, role, app_user
            """);
        command.Parameters.Add(new NpgsqlParameter("actor_id", actorId));
        return await command.ExecuteScalarAsync(token) is Guid;
    }

    private static string GetErrorMessage(Exception exception)
    {
        if (exception is DbUpdateConcurrencyException concurrencyEx)
        {
            var entityDetails = string.Join("; ", concurrencyEx.Entries.Select(e =>
            {
                var modProps = string.Join(", ", e.Properties.Where(p => p.IsModified).Select(p => $"{p.Metadata.Name} (orig: {p.OriginalValue}, curr: {p.CurrentValue})"));
                var keys = string.Join(", ", e.Metadata.FindPrimaryKey()?.Properties.Select(p => $"{p.Name}={e.Property(p.Name).CurrentValue}") ?? []);
                return $"{e.Entity.GetType().Name} (state: {e.State}, key: [{keys}], modified: [{modProps}])";
            }));
            return $"Concurrency on [{entityDetails}]: {exception.GetBaseException().Message}";
        }
        return exception.GetBaseException().Message;
    }

    private static PrepareCaptureAttemptResult PrepareFailure(string code, string message) =>
        new(false, Guid.Empty, 0, Guid.Empty, "UNCHANGED", 0, code, message);
    private static PrepareCaptureAttemptResult PrepareException(Exception exception) =>
        PrepareFailure("CAPTURE_PREPARE_CONFLICT", GetErrorMessage(exception));
    private static StartCaptureFlightResult StartFailure(string code, string message) =>
        new(false, Guid.Empty, "UNCHANGED", 0, default, code, message);
    private static StartCaptureFlightResult StartException(Exception exception) =>
        StartFailure("CAPTURE_START_CONFLICT", GetErrorMessage(exception));
    private static EndCaptureFlightResult EndFailure(string code, string message) =>
        new(false, Guid.Empty, "UNCHANGED", 0, default, code, message);
    private static EndCaptureFlightResult EndException(Exception exception) =>
        EndFailure("CAPTURE_END_CONFLICT", GetErrorMessage(exception));
    private static RecordFlightFailureResult FailureFailure(string code, string message) =>
        new(false, Guid.Empty, "UNCHANGED", 0, code, message);
    private static RecordFlightFailureResult FailureException(Exception exception) =>
        FailureFailure("CAPTURE_FAILURE_CONFLICT", GetErrorMessage(exception));
    private static RecoverCaptureMissionResult RecoverMissionFailure(string code, string message) =>
        new(false, Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty, 0, false, code, message);
    private static ApproveCaptureInputResult ApprovalFailure(string code, string message) =>
        new(false, Guid.Empty, Guid.Empty, "UNCHANGED", 0, code, message);
    private static ApproveCaptureInputResult ApprovalException(Exception exception) =>
        ApprovalFailure("CAPTURE_INPUT_CONFLICT", GetErrorMessage(exception));
    private static ReopenCaptureInputManifestResult ReopenFailure(string code, string message) =>
        new(false, Guid.Empty, Guid.Empty, Guid.Empty, "UNCHANGED", 0, 0, code, message);
    private static FreezeCaptureInputManifestResult ManifestFailure(string code, string message) =>
        new(false, Guid.Empty, Guid.Empty, string.Empty, 0, 0L, "UNCHANGED", 0, code, message);
    private static FreezeCaptureInputManifestResult ManifestException(Exception exception) =>
        ManifestFailure("CAPTURE_MANIFEST_CONFLICT", GetErrorMessage(exception));
    private static CreateQualityReflightResult ReflightFailure(string code, string message) =>
        new(false, Guid.Empty, 0, Guid.Empty, "UNCHANGED", 0, code, message);
    private static CreateQualityReflightResult ReflightException(Exception exception) =>
        ReflightFailure("REFLIGHT_CONFLICT", GetErrorMessage(exception));
    private static CancelCaptureOrderResult CancelFailure(
        Guid orderId,
        string code,
        string message) =>
        new(false, orderId, "UNCHANGED", 0m, 0m, null, null, code, message);
    private static CancelCaptureOrderResult CancelException(
        Guid orderId,
        Exception exception) =>
        exception is SurveyDomainException domainException
            ? CancelFailure(orderId, domainException.Code, domainException.Message)
            : CancelFailure(orderId, "CANCELLATION_CONFLICT", GetErrorMessage(exception));
}
