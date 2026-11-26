using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using AgriDrone.Modules.Missions.Application.Features.Media;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Surveys.Application.Features.Appointments.ProposeSurveyAppointment;
using AgriDrone.Modules.Surveys.Domain;
using NetTopologySuite.Geometries;
using Xunit;

namespace AgriDrone.UnitTests.Application.Surveys;

/// <summary>
/// Unit &amp; validator tests for F-02, F-03, F-04, and CaptureAttempt transitions:
/// - TC-F02-A01: ProposeSurveyAppointmentCommandValidator rejects EndAt &lt;= StartAt.
/// - TC-F03-U02: RecordFlightCompleted handles endedAt == startedAt and blocks endedAt &lt; startedAt.
/// - TC-F03-A01: StartFlight rejects missing/invalid preflight checklist evidence.
/// - TC-F04-U02: MissionUploadReadinessPolicy accepts min=1 raw image and blocks 0 images.
/// - TC-ST-CAP-03: CaptureAttempt Processing -&gt; Accepted on valid acceptance evidence.
/// </summary>
public sealed class V3AppointmentAndFlightValidationTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 15, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();

    // ─── TC-F02-A01 ────────────────────────────────────────────────────────────

    [Fact, Trait("TC", "TC-F02-A01")]
    public void ProposeAppointmentValidatorRejectsEndEqualOrBeforeStartAndAcceptsValidWindow()
    {
        var validator = new ProposeSurveyAppointmentCommandValidator();
        var start = T0.AddHours(2);

        var equalWindow = new ProposeSurveyAppointmentCommand(
            OrderId, SurveyAppointmentPurpose.Capture, start, start, 1u, null, null, ContractScope.V3);
        Assert.False(validator.Validate(equalWindow).IsValid);

        var earlierEnd = new ProposeSurveyAppointmentCommand(
            OrderId, SurveyAppointmentPurpose.Capture, start, start.AddMinutes(-1), 1u, null, null, ContractScope.V3);
        Assert.False(validator.Validate(earlierEnd).IsValid);

        var validWindow = new ProposeSurveyAppointmentCommand(
            OrderId, SurveyAppointmentPurpose.Capture, start, start.AddHours(2), 1u, null, null, ContractScope.V3);
        Assert.True(validator.Validate(validWindow).IsValid);
    }

    // ─── TC-F03-U02 ────────────────────────────────────────────────────────────

    [Fact, Trait("TC", "TC-F03-U02")]
    public void FlightCompletionAllowsEqualStartEndAndRejectsEarlierEndWithoutMutatingHistory()
    {
        var missionId = Guid.NewGuid();
        var corrId = Guid.NewGuid();
        var startedAt = T0.AddMinutes(10);

        // Equal end == start -> allowed per domain monotonic timestamp rule (>= UpdatedAt)
        var attemptEqual = CreateInFlightAttempt(missionId, startedAt, corrId);
        var historyBeforeEqual = attemptEqual.History.Count;
        attemptEqual.RecordFlightCompleted(
            startedAt, "receipt-eq", "Mobile", "Completed immediately", ActorId, corrId);
        Assert.Equal(CaptureAttemptStatus.AwaitingUpload, attemptEqual.Status);
        Assert.Equal(startedAt, attemptEqual.EndedAt);
        Assert.Equal(historyBeforeEqual + 1, attemptEqual.History.Count);

        // Earlier by 1 tick -> blocked and leaves Status == InFlight & history unchanged
        var attemptEarlier = CreateInFlightAttempt(missionId, startedAt, corrId);
        var historyBeforeEarlier = attemptEarlier.History.Count;
        Assert.Throws<ArgumentException>(() =>
            attemptEarlier.RecordFlightCompleted(
                startedAt.AddTicks(-1), "receipt-bad", "Mobile", "Earlier end", ActorId, corrId));
        Assert.Equal(CaptureAttemptStatus.InFlight, attemptEarlier.Status);
        Assert.Null(attemptEarlier.EndedAt);
        Assert.Equal(historyBeforeEarlier, attemptEarlier.History.Count);
    }

    // ─── TC-F03-A01 ────────────────────────────────────────────────────────────

    [Fact, Trait("TC", "TC-F03-A01")]
    public void StartFlightRejectsMissingChecklistEvidenceAndUnreadyAttempt()
    {
        var corrId = Guid.NewGuid();
        var attempt = CaptureAttempt.CreateInitial(
            Guid.NewGuid(), OrderId, TenantId, FarmId, Guid.NewGuid(),
            ActorId, "Manager", "Init", corrId, operationId: null, payloadFingerprint: null, createdAt: T0);

        // Empty checklist reference rejected
        var evidenceEx = Assert.Throws<SurveyDomainException>(() =>
            attempt.MarkPreflightReady("   ", ActorId, "Mobile", "Missing checklist", corrId, timestamp: T0.AddMinutes(1)));
        Assert.Equal(CaptureAttemptDomainErrorCodes.PreflightEvidenceRequired, evidenceEx.Code);
        Assert.Equal(CaptureAttemptStatus.Planned, attempt.Status);

        // Starting directly from Planned (no checklist completed) rejected
        var startEx = Assert.Throws<SurveyDomainException>(() =>
            attempt.StartFlight(attempt.MissionId!.Value, T0.AddMinutes(2), "Mobile", "Bypass", "ticket", ActorId, corrId));
        Assert.Equal(CaptureAttemptDomainErrorCodes.InvalidTransition, startEx.Code);
        Assert.Equal(CaptureAttemptStatus.Planned, attempt.Status);
    }

    // ─── TC-F04-U02 ────────────────────────────────────────────────────────────

    [Fact, Trait("TC", "TC-F04-U02")]
    public void UploadReadinessPolicyRequiresAtLeastOneImageAndAllowsSingleValidImage()
    {
        var mission = CreateUploadingMissionWithRoute();

        var oneImageSnapshot = new MissionUploadReadinessSnapshot(
            RawImageCount: 1,
            RawVideoCount: 0,
            HasTelemetryImport: true,
            ImportedPointCount: 2,
            PersistedPointCount: 2,
            HasActiveUploadSessions: false);

        var errorsWithOne = MissionUploadReadinessPolicy.Evaluate(mission, oneImageSnapshot);
        Assert.Empty(errorsWithOne);

        var zeroImagesSnapshot = oneImageSnapshot with { RawImageCount = 0 };
        var errorsWithZero = MissionUploadReadinessPolicy.Evaluate(mission, zeroImagesSnapshot);
        Assert.Single(errorsWithZero);
        Assert.Equal("MissionUpload.RequiredMediaMissing", errorsWithZero[0].Code);
    }

    // ─── TC-ST-CAP-03 ──────────────────────────────────────────────────────────

    [Fact, Trait("TC", "TC-ST-CAP-03")]
    public void CaptureAttemptProcessingTransitionsToAcceptedWithProvenanceEvidence()
    {
        var missionId = Guid.NewGuid();
        var corrId = Guid.NewGuid();
        var attempt = CreateInFlightAttempt(missionId, T0.AddMinutes(5), corrId);

        attempt.RecordFlightCompleted(T0.AddMinutes(20), "flight-receipt-1", "Mobile", "Done", ActorId, corrId);
        attempt.MarkUploaded(
            new CaptureUploadManifestEvidence(TenantId, FarmId, OrderId, attempt.Id, missionId, "manifest-1", "sha256-1"),
            T0.AddMinutes(25), "Manager", "Manifest frozen", ActorId, corrId);
        attempt.StartProcessing(
            new CaptureInputApprovalEvidence(TenantId, FarmId, OrderId, attempt.Id, missionId, "manifest-1", "sha256-1", "approval-1", "job-1"),
            T0.AddMinutes(30), "Manager", "Input approved", ActorId, corrId);
        Assert.Equal(CaptureAttemptStatus.Processing, attempt.Status);

        attempt.AcceptProcessing(
            new CaptureProcessingAcceptanceEvidence(TenantId, FarmId, OrderId, attempt.Id, missionId, "job-1", "receipt-1"),
            T0.AddMinutes(40), "Manager", "Freeze internal result accepted", ActorId, corrId);
        Assert.Equal(CaptureAttemptStatus.Accepted, attempt.Status);
    }

    private static CaptureAttempt CreateInFlightAttempt(Guid missionId, DateTimeOffset startedAt, Guid corrId)
    {
        var attempt = CaptureAttempt.CreateInitial(
            Guid.NewGuid(), OrderId, TenantId, FarmId, missionId,
            ActorId, "Manager", "Init", corrId, operationId: null, payloadFingerprint: null, createdAt: T0);
        attempt.MarkPreflightReady("checklist-ref-1", ActorId, "Mobile", "Preflight OK", corrId, timestamp: T0.AddMinutes(2));
        attempt.StartFlight(missionId, startedAt, "Mobile", "Takeoff", "ticket-1", ActorId, corrId);
        return attempt;
    }

    private static DroneMission CreateUploadingMissionWithRoute()
    {
        using var parameters = JsonDocument.Parse("{}");
        var mission = DroneMission.CreateOrderBound(
            OrderId, TenantId, FarmId, [], Guid.NewGuid(), Guid.NewGuid(), ActorId,
            "MIS-BVA-01", MissionPurpose.BaselineMapping, Guid.NewGuid(),
            null, false, parameters, ActorId, T0);

        mission.Schedule(T0.AddMinutes(5), T0.AddHours(1), T0.AddMinutes(1));
        using var answers = JsonDocument.Parse("{\"batteryOk\":true}");
        mission.CompletePreflight(Guid.NewGuid(), "v1", answers, true, null, ActorId, T0.AddMinutes(2));
        mission.StartFlight(ActorId, T0.AddMinutes(6));
        mission.CompleteFlight(T0.AddMinutes(20));
        mission.StartUploading(T0.AddMinutes(21));

        var route = new LineString([
            new Coordinate(106.7000, 10.7700),
            new Coordinate(106.7005, 10.7705)
        ]) { SRID = 4326 };
        mission.SetActualFlightRoute(route, T0.AddMinutes(22));
        return mission;
    }
}
