using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.Modules.Identity.Domain.SystemManagers;
using AgriDrone.Modules.Missions.Domain.Drones;
using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Domain.Media;
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
                (!order.PreviousCompatibleOrderId.HasValue ||
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
            var appointment = await db.Set<SurveyAppointment>()
                .AsNoTracking()
                .SingleOrDefaultAsync(value =>
                    value.SurveyOrderId == order.Id &&
                    value.Purpose == SurveyAppointmentPurpose.Capture &&
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
                .SingleOrDefaultAsync(value => value.Id == request.AttemptId, token);
            var manifest = await db.Set<CaptureInputManifest>()
                .SingleOrDefaultAsync(value =>
                    value.Id == request.ManifestId &&
                    value.CaptureAttemptId == request.AttemptId &&
                    value.SurveyOrderId == request.OrderId,
                    token);
            var mission = attempt is null
                ? null
                : await db.Set<DroneMission>().AsNoTracking().SingleOrDefaultAsync(
                    value => value.CaptureAttemptId == attempt.Id, token);
            if (order is null || attempt is null || manifest is null || mission is null)
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
            var replayFingerprint = await ReadOperationFingerprintAsync(
                attempt.Id, request.OperationId, token);
            if (replayFingerprint is not null)
            {
                var replayApprovalId = await db.Set<CaptureInputApproval>()
                    .AsNoTracking()
                    .Where(value =>
                        value.CaptureAttemptId == attempt.Id &&
                        value.InputManifestId == manifest.Id)
                    .Select(value => value.Id)
                    .SingleOrDefaultAsync(token);
                return string.Equals(replayFingerprint, request.PayloadFingerprint, StringComparison.Ordinal) &&
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
                !string.Equals(manifest.ManifestFingerprint, request.ManifestFingerprint, StringComparison.Ordinal))
            {
                return ApprovalFailure("INPUT_VERSION_CONFLICT", "Uploaded Attempt or exact manifest is missing or stale.");
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
                order.Scope != ContractScope.V3)
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
            var replayFingerprint = await ReadOperationFingerprintAsync(
                attempt.Id, request.OperationId, token);
            if (replayFingerprint is not null)
            {
                var replayManifest = await db.Set<CaptureInputManifest>()
                    .AsNoTracking()
                    .Where(value => value.CaptureAttemptId == attempt.Id)
                    .OrderByDescending(value => value.CreatedAt)
                    .FirstOrDefaultAsync(token);
                return string.Equals(replayFingerprint, request.PayloadFingerprint, StringComparison.Ordinal) &&
                       replayManifest is not null
                    ? new FreezeCaptureInputManifestResult(
                        true, attempt.Id, replayManifest.Id,
                        replayManifest.ManifestFingerprint,
                        replayManifest.TotalFileCount,
                        replayManifest.TotalSizeBytes,
                        attempt.Status.ToString(),
                        attempt.Version)
                    : ManifestFailure("OPERATION_CONFLICT", "The operation identifier was already used with another payload.");
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

            var files = await db.Set<MissionMedia>()
                .AsNoTracking()
                .Where(value =>
                    value.MissionId == mission.Id &&
                    value.Media.StorageStatus == MediaStorageStatus.Active &&
                    value.Media.DeletedAt == null)
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

            var canonical = string.Join(
                '\n',
                files.Select(value =>
                    $"{value.MediaId:D}|{value.Checksum!.ToLowerInvariant()}|{value.FileSizeBytes}|{value.Role}"));
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
                $"files={files.Count}");
            db.Set<CaptureInputManifest>().Add(manifest);

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
            return new FreezeCaptureInputManifestResult(
                true,
                attempt.Id,
                manifest.Id,
                manifest.ManifestFingerprint,
                manifest.TotalFileCount,
                manifest.TotalSizeBytes,
                attempt.Status.ToString(),
                attempt.Version);
        },
        ManifestException,
        cancellationToken);

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
            if (drone is null ||
                !drone.IsOperationalFor(request.ProposedStartAt, request.ProposedEndAt) ||
                commercial is null ||
                (commercial.MapsPoles && !drone.SupportsPurpose(MissionPurpose.BaselineMapping)) ||
                (commercial.ScreensDisease && !drone.SupportsPurpose(MissionPurpose.PlantHealth)))
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

            var latestAppointment = await db.Set<SurveyAppointment>()
                .Where(value =>
                    value.SurveyOrderId == order.Id &&
                    value.Purpose == SurveyAppointmentPurpose.Capture)
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

            var appointment = SurveyAppointment.Create(
                order.Id,
                SurveyAppointmentPurpose.Capture,
                (latestAppointment?.RevisionNumber ?? 0) + 1,
                latestAppointment?.Id,
                request.ActorId,
                request.ProposedStartAt,
                request.ProposedEndAt,
                now);

            var successorId = Guid.CreateVersion7();
            using var flightParameters = JsonSerializer.SerializeToDocument(new
            {
                contract = "SURVEY_CAPTURE_V3_REFLIGHT",
                orderId = order.Id,
                captureAttemptId = successorId,
                parentAttemptId = parent.Id,
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
                $"V3R{parent.AttemptIndex + 1}-{order.Id:N}",
                MissionPurpose.Capture,
                request.OperationId,
                order.FarmBaseMapVersionId,
                requiresBaselineCompletion: false,
                flightParameters,
                request.ActorId,
                now);
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
                now);
            mission.BindCaptureAttempt(successor.Id);
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
            if (!await LockOrderAsync(request.OrderId, token))
            {
                return CancelFailure(request.OrderId, "ORDER_NOT_FOUND", "Survey Order was not found.");
            }
            var now = clock.GetUtcNow();
            var order = await db.Set<SurveyOrder>().SingleAsync(
                value => value.Id == request.OrderId, token);
            var decisionKind = request.DecisionKind switch
            {
                CaptureCancellationDecisionKind.CustomerBeforeCapture => CancellationDecisionKind.CustomerBeforeCapture,
                CaptureCancellationDecisionKind.CustomerAfterAcceptableCapture => CancellationDecisionKind.CustomerAfterAcceptableCapture,
                CaptureCancellationDecisionKind.OperationalPostponement => CancellationDecisionKind.OperationalPostponement,
                CaptureCancellationDecisionKind.ServiceFailure => CancellationDecisionKind.ServiceFailure,
                _ => throw new InvalidOperationException("Unsupported cancellation decision kind.")
            };
            var ownerDecision = decisionKind == CancellationDecisionKind.CustomerBeforeCapture;
            if (ownerDecision)
            {
                if (request.ActorId != request.RequesterId ||
                    !await HasOwnerAccessAsync(order.TenantId, request.ActorId, token))
                {
                    return CancelFailure(order.Id, "OWNER_FORBIDDEN", "Before-capture cancellation requires the active Tenant Owner.");
                }
            }
            else if (!await IsSystemAdminAsync(request.ActorId, token))
            {
                return CancelFailure(order.Id, "ADMIN_DECISION_REQUIRED", "This cancellation decision requires a System Admin.");
            }

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

            var receipts = await db.Set<SurveyFundingReceipt>()
                .Where(value => value.SurveyOrderId == order.Id)
                .SumAsync(value => (decimal?)value.Amount, token) ?? 0m;
            var completedRefunds = await db.Set<SurveyRefundObligation>()
                .Where(value =>
                    value.SurveyOrderId == order.Id &&
                    value.Status == SurveyRefundObligationStatus.Completed)
                .SumAsync(value => (decimal?)value.Amount, token) ?? 0m;
            var activeReservations = await db.Set<SurveyRefundObligation>()
                .Where(value =>
                    value.SurveyOrderId == order.Id &&
                    (value.Status == SurveyRefundObligationStatus.Pending ||
                     value.Status == SurveyRefundObligationStatus.Reserved ||
                     value.Status == SurveyRefundObligationStatus.Executing))
                .SumAsync(value => (decimal?)value.Amount, token) ?? 0m;
            var unreserved = receipts - completedRefunds - activeReservations;
            if (unreserved < 0m)
            {
                return CancelFailure(order.Id, "REFUND_RECONCILIATION_HOLD", "Existing refunds and reservations exceed confirmed receipts.");
            }
            var retained = request.RequestedRetentionAmount;
            var refund = unreserved - retained;
            var terms = PinnedCancellationPolicyTerms.Create(
                policy.Id,
                policy.VersionNumber,
                policy.RefundableBeforeCapture,
                policy.RetainDepositAfterCapture,
                policy.AllowedFreeNoShowReschedules,
                policy.OperationalPostponementRefundable,
                policy.ServiceFailureRefundable);
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
                receipts,
                completedRefunds,
                activeReservations,
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

            if (order.DepositObligationId.HasValue)
            {
                var deposit = await db.Set<SurveyDepositObligation>().SingleOrDefaultAsync(value =>
                    value.Id == order.DepositObligationId.Value &&
                    value.SurveyOrderId == order.Id,
                    token);
                deposit?.Cancel(now);
            }
            var activeAppointments = await db.Set<SurveyAppointment>()
                .Where(value =>
                    value.SurveyOrderId == order.Id &&
                    value.Purpose == SurveyAppointmentPurpose.Capture &&
                    (value.Status == SurveyAppointmentStatus.Proposed ||
                     value.Status == SurveyAppointmentStatus.Confirmed ||
                     value.Status == SurveyAppointmentStatus.RescheduleRequested))
                .ToListAsync(token);
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
                decision.Id, refundObligation?.Id);
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
        Guid? refundObligationId = null)
    {
        using var newData = JsonSerializer.SerializeToDocument(new
        {
            orderStatus = order.Status.ToString(),
            attemptId,
            missionId,
            cancellationDecisionId,
            refundObligationId
        });
        db.Set<AuditLog>().Add(AuditLog.ForUserAction(
            order.TenantId,
            order.FarmId,
            actorId,
            correlationId,
            nameof(SurveyOrder),
            order.Id,
            changeType,
            oldData: null,
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
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
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
    private static ApproveCaptureInputResult ApprovalFailure(string code, string message) =>
        new(false, Guid.Empty, Guid.Empty, "UNCHANGED", 0, code, message);
    private static ApproveCaptureInputResult ApprovalException(Exception exception) =>
        ApprovalFailure("CAPTURE_INPUT_CONFLICT", GetErrorMessage(exception));
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
        CancelFailure(orderId, "CANCELLATION_CONFLICT", GetErrorMessage(exception));
}
