using System.Text.Json;
using AgriDrone.Modules.Surveys.Domain;
using Xunit;

namespace AgriDrone.UnitTests.Domain.Surveys;

public sealed class CaptureAttemptTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 11, 13, 8, 0, 0, TimeSpan.Zero);

    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid FarmId = Guid.NewGuid();
    private static readonly Guid OrderId = Guid.NewGuid();
    private static readonly Guid ActorId = Guid.NewGuid();

    private static CaptureAttempt CreatePlannedAttempt(
        Guid? attemptId = null,
        Guid? orderId = null,
        Guid? tenantId = null,
        Guid? farmId = null,
        Guid? missionId = null)
    {
        return CaptureAttempt.CreateInitial(
            attemptId ?? Guid.NewGuid(),
            orderId ?? OrderId,
            tenantId ?? TenantId,
            farmId ?? FarmId,
            missionId,
            ActorId,
            "Coordinator",
            "Initial attempt planning",
            Guid.NewGuid(),
            operationId: null,
            payloadFingerprint: null,
            createdAt: Now);
    }

    private static CaptureUploadManifestEvidence UploadEvidence(CaptureAttempt attempt, string manifestId) =>
        new(attempt.TenantId, attempt.FarmId, attempt.OrderId, attempt.Id,
            attempt.MissionId ?? throw new InvalidOperationException("Test attempt must have a bound Mission."),
            manifestId, "test-manifest-sha256");

    private static CaptureInputApprovalEvidence InputEvidence(
        CaptureAttempt attempt, string approvalId, string jobId) =>
        new(attempt.TenantId, attempt.FarmId, attempt.OrderId, attempt.Id,
            attempt.MissionId ?? throw new InvalidOperationException("Test attempt must have a bound Mission."),
            attempt.UploadManifestReference ?? throw new InvalidOperationException("Test manifest is missing."),
            attempt.UploadManifestFingerprint ?? throw new InvalidOperationException("Test fingerprint is missing."),
            approvalId, jobId);

    private static CaptureProcessingAcceptanceEvidence AcceptanceEvidence(
        CaptureAttempt attempt, string receiptId) =>
        new(attempt.TenantId, attempt.FarmId, attempt.OrderId, attempt.Id,
            attempt.MissionId ?? throw new InvalidOperationException("Test attempt must have a bound Mission."),
            attempt.ProcessingJobReference ?? throw new InvalidOperationException("Test job is missing."),
            receiptId);

    [Fact]
    public void InitialAttemptHasCorrectPlannedStateAndHistory()
    {
        var id = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();

        var attempt = CaptureAttempt.CreateInitial(
            id,
            OrderId,
            TenantId,
            FarmId,
            missionId,
            ActorId,
            "Coordinator",
            "Initial attempt planning",
            correlationId,
            operationId: null,
            payloadFingerprint: null,
            createdAt: Now);

        Assert.Equal(id, attempt.Id);
        Assert.Equal(OrderId, attempt.OrderId);
        Assert.Equal(TenantId, attempt.TenantId);
        Assert.Equal(FarmId, attempt.FarmId);
        Assert.Equal(1, attempt.AttemptIndex);
        Assert.Equal(CaptureAttemptStatus.Planned, attempt.Status);
        Assert.Equal(1u, attempt.Version);
        Assert.Equal(missionId, attempt.MissionId);
        Assert.Null(attempt.SupersededAttemptId);
        Assert.False(attempt.IsSuccessor);
        Assert.False(attempt.IsTerminal);
        Assert.Equal(Now, attempt.CreatedAt);
        Assert.Equal(Now, attempt.UpdatedAt);
        Assert.NotNull(attempt.LastPredecessorHistoryId);

        Assert.Single(attempt.History);
        var initialHistory = attempt.History.First();
        Assert.Equal(attempt.Id, initialHistory.CaptureAttemptId);
        Assert.Equal(1u, initialHistory.TransitionSequence);
        Assert.Equal(CaptureAttemptAction.CreateInitial, initialHistory.Action);
        Assert.Null(initialHistory.FromStatus);
        Assert.Equal(CaptureAttemptStatus.Planned, initialHistory.ToStatus);
        Assert.Equal("Coordinator", initialHistory.Source);
        Assert.Equal("Initial attempt planning", initialHistory.Reason);
        Assert.Null(initialHistory.PredecessorHistoryId);
        Assert.Equal(correlationId, initialHistory.CorrelationId);
        Assert.Equal(Now, initialHistory.CreatedAt);
    }

    [Fact]
    public void FullSuccessLifecycleTransitionsToAcceptedWithImmutableHistories()
    {
        var attempt = CreatePlannedAttempt();
        var missionId = Guid.NewGuid();
        var corrId = Guid.NewGuid();

        // 1. Planned -> PreflightReady
        attempt.MarkPreflightReady(
            "preflight-check-v1",
            ActorId,
            "PreflightInspection",
            "Hardware checklist verified",
            corrId,
            timestamp: Now.AddMinutes(5));

        Assert.Equal(CaptureAttemptStatus.PreflightReady, attempt.Status);
        Assert.Equal(2u, attempt.Version);
        Assert.Equal("preflight-check-v1", attempt.PreflightEvidenceReference);

        // 2. PreflightReady -> InFlight
        attempt.StartFlight(
            missionId,
            Now.AddMinutes(10),
            "StartCoordinator",
            "Flight authorized and airborne",
            "auth-ticket-001",
            ActorId,
            corrId);

        Assert.Equal(CaptureAttemptStatus.InFlight, attempt.Status);
        Assert.Equal(3u, attempt.Version);
        Assert.Equal(missionId, attempt.MissionId);
        Assert.Equal(Now.AddMinutes(10), attempt.StartedAt);

        // 3. InFlight -> AwaitingUpload
        attempt.RecordFlightCompleted(
            Now.AddMinutes(35),
            "flight-receipt-001",
            "FlightTelemetry",
            "Drone touched down safely",
            ActorId,
            corrId);

        Assert.Equal(CaptureAttemptStatus.AwaitingUpload, attempt.Status);
        Assert.Equal(4u, attempt.Version);
        Assert.Equal(Now.AddMinutes(35), attempt.EndedAt);
        Assert.Equal("flight-receipt-001", attempt.FlightCompletionReceipt);

        // 4. AwaitingUpload -> Uploaded
        var uploadEvidence = new CaptureUploadManifestEvidence(
            TenantId,
            FarmId,
            OrderId,
            attempt.Id,
            missionId,
            "manifest-hash-abc",
            "sha256-fingerprint-123");

        attempt.MarkUploaded(
            uploadEvidence,
            Now.AddMinutes(45),
            "MediaIngest",
            "All raw images and GNSS verified",
            ActorId,
            corrId);

        Assert.Equal(CaptureAttemptStatus.Uploaded, attempt.Status);
        Assert.Equal(5u, attempt.Version);
        Assert.Equal("manifest-hash-abc", attempt.UploadManifestReference);
        Assert.Equal("sha256-fingerprint-123", attempt.UploadManifestFingerprint);

        // 5. Uploaded -> Processing (Requires InputApproval, but does NOT set Accepted)
        var inputEvidence = new CaptureInputApprovalEvidence(
            TenantId,
            FarmId,
            OrderId,
            attempt.Id,
            missionId,
            "manifest-hash-abc",
            "sha256-fingerprint-123",
            "approval-input-001",
            "proc-job-xyz");

        attempt.StartProcessing(
            inputEvidence,
            Now.AddMinutes(50),
            "Coordinator",
            "Input quality confirmed by Manager, job submitted",
            ActorId,
            corrId);

        Assert.Equal(CaptureAttemptStatus.Processing, attempt.Status);
        Assert.Equal(6u, attempt.Version);
        Assert.Equal("approval-input-001", attempt.InputApprovalReference);
        Assert.Equal("proc-job-xyz", attempt.ProcessingJobReference);
        Assert.False(attempt.IsTerminal);

        // 6. Processing -> Accepted (Terminal acceptance from processing service)
        var acceptanceEvidence = new CaptureProcessingAcceptanceEvidence(
            TenantId,
            FarmId,
            OrderId,
            attempt.Id,
            missionId,
            "proc-job-xyz",
            "review-receipt-final");

        attempt.AcceptProcessing(
            acceptanceEvidence,
            Now.AddHours(2),
            "ProcessingService",
            "Orthophoto stitching and AI model accepted",
            ActorId,
            corrId);

        Assert.Equal(CaptureAttemptStatus.Accepted, attempt.Status);
        Assert.Equal(7u, attempt.Version);
        Assert.Equal("review-receipt-final", attempt.ProcessingAcceptanceReceipt);
        Assert.True(attempt.IsTerminal);

        // Verify history chain
        Assert.Equal(7, attempt.History.Count);
        var histories = attempt.History.ToList();
        for (var i = 0; i < histories.Count; i++)
        {
            Assert.Equal((uint)(i + 1), histories[i].TransitionSequence);
            if (i > 0)
            {
                Assert.Equal(histories[i - 1].Id, histories[i].PredecessorHistoryId);
                Assert.Equal(histories[i - 1].ToStatus, histories[i].FromStatus);
            }
        }
    }

    [Fact]
    public void PreflightCanBeInvalidatedAndReMarkedReady()
    {
        var attempt = CreatePlannedAttempt();
        var corrId = Guid.NewGuid();

        attempt.MarkPreflightReady(
            "preflight-evidence-1",
            ActorId,
            "Inspection",
            "Ready for takeoff",
            corrId,
            timestamp: Now.AddMinutes(5));
        Assert.Equal(CaptureAttemptStatus.PreflightReady, attempt.Status);

        // Invalidate before flight
        attempt.InvalidatePreflight(
            "Wind gust exceeded safety threshold",
            "weather-station-telemetry",
            ActorId,
            "SafetySupervisor",
            corrId,
            timestamp: Now.AddMinutes(7));

        Assert.Equal(CaptureAttemptStatus.Planned, attempt.Status);
        Assert.Equal(3u, attempt.Version);

        // Re-inspect and mark ready again
        attempt.MarkPreflightReady(
            "preflight-evidence-2",
            ActorId,
            "Inspection",
            "Weather clear, re-inspected",
            corrId,
            timestamp: Now.AddMinutes(20));

        Assert.Equal(CaptureAttemptStatus.PreflightReady, attempt.Status);
        Assert.Equal(4u, attempt.Version);
        Assert.Equal("preflight-evidence-2", attempt.PreflightEvidenceReference);
        Assert.Equal(4, attempt.History.Count);
    }

    [Fact]
    public void FlightFailedFromInFlightSetsTerminalAndEndedAt()
    {
        var attempt = CreatePlannedAttempt();
        var missionId = Guid.NewGuid();
        var corrId = Guid.NewGuid();

        attempt.MarkPreflightReady("preflight-ok", ActorId, "Inspection", "OK", corrId, timestamp: Now.AddMinutes(1));
        attempt.StartFlight(missionId, Now.AddMinutes(5), "Coordinator", "Start", "ticket-1", ActorId, corrId);

        // Flight failure
        attempt.RecordFlightFailed(
            Now.AddMinutes(15),
            "incident-report-battery-drop",
            "DroneMonitor",
            "Critical motor voltage drop, forced RTH crash landing",
            ActorId,
            corrId);

        Assert.Equal(CaptureAttemptStatus.FlightFailed, attempt.Status);
        Assert.Equal(4u, attempt.Version);
        Assert.True(attempt.IsTerminal);
        Assert.Equal(Now.AddMinutes(15), attempt.EndedAt);
        Assert.Equal("incident-report-battery-drop", attempt.FlightFailureEvidence);

        // Terminal attempt cannot be modified
        var ex = Assert.Throws<SurveyDomainException>(() =>
            attempt.MarkPreflightReady("new-preflight", ActorId, "Coord", "Retry", corrId));
        Assert.Equal(CaptureAttemptDomainErrorCodes.AttemptTerminal, ex.Code);
    }

    [Fact]
    public void QualityReflightSuccessorCreationChainsCorrectly()
    {
        var attempt1 = CreatePlannedAttempt();
        var mission1 = Guid.NewGuid();
        var corrId = Guid.NewGuid();
        var approverId = Guid.NewGuid();

        attempt1.MarkPreflightReady("preflight-1", ActorId, "Inspection", "OK", corrId, timestamp: Now.AddMinutes(1));
        attempt1.StartFlight(mission1, Now.AddMinutes(5), "Coordinator", "Start", "ticket-1", ActorId, corrId);
        attempt1.RecordFlightFailed(Now.AddMinutes(15), "motor-failure-log", "Monitor", "Motor stopped", ActorId, corrId);

        // Create successor attempt 2 from failed attempt 1
        var successorId = Guid.NewGuid();
        var attempt2 = CaptureAttempt.CreateSuccessor(
            successorId,
            attempt1,
            missionId: null,
            approverId,
            "Approved free quality re-flight due to hardware motor failure",
            "incident-ticket-456",
            ActorId,
            "Coordinator",
            corrId,
            operationId: null,
            payloadFingerprint: null,
            createdAt: Now.AddMinutes(30));

        Assert.Equal(successorId, attempt2.Id);
        Assert.Equal(attempt1.OrderId, attempt2.OrderId);
        Assert.Equal(attempt1.TenantId, attempt2.TenantId);
        Assert.Equal(attempt1.FarmId, attempt2.FarmId);
        Assert.Equal(2, attempt2.AttemptIndex);
        Assert.Equal(attempt1.Id, attempt2.SupersededAttemptId);
        Assert.Equal(mission1, attempt2.ParentMissionId);
        Assert.True(attempt2.IsSuccessor);
        Assert.Equal(approverId, attempt2.ReflightApprovedBy);
        Assert.Equal("Approved free quality re-flight due to hardware motor failure", attempt2.ReflightApprovalReason);
        Assert.Equal(Now.AddMinutes(30), attempt2.ReflightApprovedAt);
        Assert.Equal(CaptureAttemptStatus.Planned, attempt2.Status);

        // Parent attempt status was NOT modified
        Assert.Equal(CaptureAttemptStatus.FlightFailed, attempt1.Status);

        // Attempt 2 flies, completes flight, uploads images, but quality check demands another re-flight
        var mission2 = Guid.NewGuid();
        attempt2.MarkPreflightReady("preflight-2", ActorId, "Inspection", "OK", corrId, timestamp: Now.AddMinutes(35));
        attempt2.StartFlight(mission2, Now.AddMinutes(40), "Coordinator", "Start", "ticket-2", ActorId, corrId);
        attempt2.RecordFlightCompleted(Now.AddMinutes(65), "receipt-2", "Telemetry", "Landed", ActorId, corrId);
        attempt2.MarkUploaded(UploadEvidence(attempt2, "manifest-2"), Now.AddMinutes(70), "Media", "Uploaded", ActorId, corrId);

        // Manager rejects quality -> ReflightRequired
        attempt2.RequireReflight(
            "cloud-shadow-coverage-over-40%",
            "Image quality unusable due to heavy cloud shadows",
            approverId,
            Now.AddMinutes(75),
            "QualityInspector",
            corrId);

        Assert.Equal(CaptureAttemptStatus.ReflightRequired, attempt2.Status);
        Assert.True(attempt2.IsTerminal);

        // Create successor attempt 3 from attempt 2
        var attempt3Id = Guid.NewGuid();
        var attempt3 = CaptureAttempt.CreateSuccessor(
            attempt3Id,
            attempt2,
            missionId: null,
            approverId,
            "Approved second re-flight due to bad cloud coverage",
            "qa-report-789",
            ActorId,
            "Coordinator",
            corrId,
            operationId: null,
            payloadFingerprint: null,
            createdAt: Now.AddMinutes(80));

        Assert.Equal(3, attempt3.AttemptIndex);
        Assert.Equal(attempt2.Id, attempt3.SupersededAttemptId);
        Assert.Equal(mission2, attempt3.ParentMissionId);
    }

    [Fact]
    public void QualityReflightSuccessorValidationFailsOnMismatchedOrderOrInvalidStatus()
    {
        var attempt = CreatePlannedAttempt();
        var approverId = Guid.NewGuid();

        // 1. Cannot create successor from non-terminal attempt (Planned)
        var ex1 = Assert.Throws<SurveyDomainException>(() =>
            CaptureAttempt.CreateSuccessor(
                Guid.NewGuid(),
                attempt,
                null,
                approverId,
                "Reason",
                "evidence-ref",
                ActorId,
                "Source",
                Guid.NewGuid(),
                null,
                null,
                Now));
        Assert.Equal(CaptureAttemptDomainErrorCodes.InvalidParentStatus, ex1.Code);

        // 2. Cannot create successor if parent is Accepted
        var acceptedAttempt = CreatePlannedAttempt();
        var missionId = Guid.NewGuid();
        var corrId = Guid.NewGuid();
        acceptedAttempt.MarkPreflightReady("preflight-ok", ActorId, "Coord", "OK", corrId, timestamp: Now.AddMinutes(1));
        acceptedAttempt.StartFlight(missionId, Now.AddMinutes(5), "Coord", "Start", null, ActorId, corrId);
        acceptedAttempt.RecordFlightCompleted(Now.AddMinutes(30), "receipt", "Coord", "Landed", ActorId, corrId);
        acceptedAttempt.MarkUploaded(UploadEvidence(acceptedAttempt, "manifest"), Now.AddMinutes(35), "Coord", "Upload", ActorId, corrId);
        acceptedAttempt.StartProcessing(InputEvidence(acceptedAttempt, "approval-1", "job-1"), Now.AddMinutes(40), "Coord", "Proc", ActorId, corrId);
        acceptedAttempt.AcceptProcessing(AcceptanceEvidence(acceptedAttempt, "review-ok"), Now.AddMinutes(60), "Coord", "Accepted", ActorId, corrId);

        var ex2 = Assert.Throws<SurveyDomainException>(() =>
            CaptureAttempt.CreateSuccessor(
                Guid.NewGuid(),
                acceptedAttempt,
                null,
                approverId,
                "Reason",
                "evidence-ref",
                ActorId,
                "Source",
                Guid.NewGuid(),
                null,
                null,
                Now.AddHours(2)));
        Assert.Equal(CaptureAttemptDomainErrorCodes.InvalidParentStatus, ex2.Code);

        // 3. Cross-Order parent mismatch fails
        var failedAttempt = CreatePlannedAttempt();
        failedAttempt.MarkPreflightReady("pf", ActorId, "C", "R", corrId, timestamp: Now.AddMinutes(1));
        failedAttempt.StartFlight(missionId, Now.AddMinutes(5), "C", "S", null, ActorId, corrId);
        failedAttempt.RecordFlightFailed(Now.AddMinutes(10), "fail", "C", "F", ActorId, corrId);

        var differentOrderId = Guid.NewGuid();
        var ex3 = Assert.Throws<SurveyDomainException>(() =>
            CaptureAttempt.CreateSuccessor(
                Guid.NewGuid(),
                differentOrderId,
                TenantId,
                FarmId,
                failedAttempt,
                null,
                approverId,
                "Reason",
                "evidence-ref",
                ActorId,
                "Source",
                corrId,
                null,
                null,
                Now.AddMinutes(20)));
        Assert.Equal(CaptureAttemptDomainErrorCodes.CrossOrderParentMismatch, ex3.Code);

        // 4. Missing reason fails
        var ex4 = Assert.Throws<SurveyDomainException>(() =>
            CaptureAttempt.CreateSuccessor(
                Guid.NewGuid(),
                failedAttempt,
                null,
                approverId,
                "  ",
                "evidence-ref",
                ActorId,
                "Source",
                corrId,
                null,
                null,
                Now.AddMinutes(20)));
        Assert.Equal(CaptureAttemptDomainErrorCodes.ReflightApprovalRequired, ex4.Code);
    }

    [Fact]
    public void SuccessorInvariantsEnforceTimestampApprovalEvidenceAndMissionDifferentiation()
    {
        var parent = CreatePlannedAttempt();
        var parentMission = Guid.NewGuid();
        var corrId = Guid.NewGuid();
        var approverId = Guid.NewGuid();

        parent.MarkPreflightReady("pf", ActorId, "C", "Ready", corrId, timestamp: Now.AddMinutes(1));
        parent.StartFlight(parentMission, Now.AddMinutes(5), "C", "Start", null, ActorId, corrId);
        parent.RecordFlightFailed(Now.AddMinutes(15), "fail", "C", "Failed", ActorId, corrId);

        // 1. Successor creation timestamp earlier than parent failure timestamp throws ArgumentException
        var exEarlier = Assert.Throws<ArgumentException>(() =>
            CaptureAttempt.CreateSuccessor(
                Guid.NewGuid(),
                parent,
                null,
                approverId,
                "Reason",
                "evidence-1",
                ActorId,
                "Coordinator",
                corrId,
                null,
                null,
                Now.AddMinutes(10))); // 10 is earlier than parent endedAt 15
        Assert.Contains("earlier", exEarlier.Message);

        // 2. Missing approval evidence throws ReflightApprovalRequired
        var exMissingEvidence = Assert.Throws<SurveyDomainException>(() =>
            CaptureAttempt.CreateSuccessor(
                Guid.NewGuid(),
                parent,
                null,
                approverId,
                "Reason",
                "   ",
                ActorId,
                "Coordinator",
                corrId,
                null,
                null,
                Now.AddMinutes(20)));
        Assert.Equal(CaptureAttemptDomainErrorCodes.ReflightApprovalRequired, exMissingEvidence.Code);

        // 3. Passing parent's mission ID at successor creation throws BoundMissionRequired
        var exSameMissionOnCreate = Assert.Throws<SurveyDomainException>(() =>
            CaptureAttempt.CreateSuccessor(
                Guid.NewGuid(),
                parent,
                parentMission,
                approverId,
                "Reason",
                "evidence-1",
                ActorId,
                "Coordinator",
                corrId,
                null,
                null,
                Now.AddMinutes(20)));
        Assert.Equal(CaptureAttemptDomainErrorCodes.BoundMissionRequired, exSameMissionOnCreate.Code);

        // 4. Passing 500-character valid approval reason succeeds without string length overflow in history
        var maxReason = new string('A', 500);
        var successor = CaptureAttempt.CreateSuccessor(
            Guid.NewGuid(),
            parent,
            null,
            approverId,
            maxReason,
            "evidence-1",
            ActorId,
            "Coordinator",
            corrId,
            null,
            null,
            Now.AddMinutes(20));

        Assert.Equal(maxReason, successor.ReflightApprovalReason);
        Assert.Equal(maxReason, successor.History.First().Reason);

        // 5. Successor attempt cannot bind parent's mission later
        var exRebindParentMission = Assert.Throws<SurveyDomainException>(() =>
            successor.BindMission(parentMission));
        Assert.Equal(CaptureAttemptDomainErrorCodes.BoundMissionRequired, exRebindParentMission.Code);

        // 6. Successor attempt cannot start with parent's mission
        successor.MarkPreflightReady("pf-2", ActorId, "C", "R", corrId, timestamp: Now.AddMinutes(25));
        var exStartParentMission = Assert.Throws<SurveyDomainException>(() =>
            successor.StartFlight(parentMission, Now.AddMinutes(30), "C", "S", null, ActorId, corrId));
        Assert.Equal(CaptureAttemptDomainErrorCodes.BoundMissionRequired, exStartParentMission.Code);
    }

    [Fact]
    public void EvidenceContextMismatchThrowsEvidenceIdentityMismatch()
    {
        var attempt = CreatePlannedAttempt();
        var missionId = Guid.NewGuid();
        var corrId = Guid.NewGuid();

        attempt.MarkPreflightReady("pf", ActorId, "C", "R", corrId, timestamp: Now.AddMinutes(1));
        attempt.StartFlight(missionId, Now.AddMinutes(5), "C", "S", null, ActorId, corrId);
        attempt.RecordFlightCompleted(Now.AddMinutes(20), "rec", "C", "D", ActorId, corrId);

        // 1. Upload manifest with different Attempt ID
        var wrongAttemptManifest = new CaptureUploadManifestEvidence(
            TenantId,
            FarmId,
            OrderId,
            Guid.NewGuid(), // wrong attempt ID
            missionId,
            "manifest-1",
            "fp-1");

        var exManifest = Assert.Throws<SurveyDomainException>(() =>
            attempt.MarkUploaded(wrongAttemptManifest, Now.AddMinutes(25), "C", "R", ActorId, corrId));
        Assert.Equal(CaptureAttemptDomainErrorCodes.EvidenceIdentityMismatch, exManifest.Code);

        // Upload valid manifest
        var validManifest = new CaptureUploadManifestEvidence(
            TenantId,
            FarmId,
            OrderId,
            attempt.Id,
            missionId,
            "manifest-1",
            "fp-1");
        attempt.MarkUploaded(validManifest, Now.AddMinutes(25), "C", "R", ActorId, corrId);

        // 2. Input approval with mismatched manifest ID
        var mismatchedManifestApproval = new CaptureInputApprovalEvidence(
            TenantId,
            FarmId,
            OrderId,
            attempt.Id,
            missionId,
            "manifest-different", // does not match pinned "manifest-1"
            "fp-1",
            "approval-1",
            "job-1");

        var exApprovalManifest = Assert.Throws<SurveyDomainException>(() =>
            attempt.StartProcessing(mismatchedManifestApproval, Now.AddMinutes(30), "C", "R", ActorId, corrId));
        Assert.Equal(CaptureAttemptDomainErrorCodes.EvidenceIdentityMismatch, exApprovalManifest.Code);

        // Start processing with valid approval
        var validApproval = new CaptureInputApprovalEvidence(
            TenantId,
            FarmId,
            OrderId,
            attempt.Id,
            missionId,
            "manifest-1",
            "fp-1",
            "approval-1",
            "job-1");
        attempt.StartProcessing(validApproval, Now.AddMinutes(30), "C", "R", ActorId, corrId);

        // 3. Processing acceptance with mismatched Job ID
        var wrongJobAcceptance = new CaptureProcessingAcceptanceEvidence(
            TenantId,
            FarmId,
            OrderId,
            attempt.Id,
            missionId,
            "job-wrong", // does not match active "job-1"
            "receipt-final");

        var exAcceptanceJob = Assert.Throws<SurveyDomainException>(() =>
            attempt.AcceptProcessing(wrongJobAcceptance, Now.AddMinutes(40), "C", "R", ActorId, corrId));
        Assert.Equal(CaptureAttemptDomainErrorCodes.EvidenceIdentityMismatch, exAcceptanceJob.Code);

        // Accept with valid acceptance
        var validAcceptance = new CaptureProcessingAcceptanceEvidence(
            TenantId,
            FarmId,
            OrderId,
            attempt.Id,
            missionId,
            "job-1",
            "receipt-final");
        attempt.AcceptProcessing(validAcceptance, Now.AddMinutes(40), "C", "R", ActorId, corrId);
        Assert.Equal(CaptureAttemptStatus.Accepted, attempt.Status);
    }

    [Theory]
    [InlineData("upload", "tenant")]
    [InlineData("upload", "farm")]
    [InlineData("upload", "order")]
    [InlineData("upload", "attempt")]
    [InlineData("upload", "mission")]
    [InlineData("processing", "tenant")]
    [InlineData("processing", "farm")]
    [InlineData("processing", "order")]
    [InlineData("processing", "attempt")]
    [InlineData("processing", "mission")]
    [InlineData("processing", "manifest")]
    [InlineData("processing", "fingerprint")]
    [InlineData("acceptance", "tenant")]
    [InlineData("acceptance", "farm")]
    [InlineData("acceptance", "order")]
    [InlineData("acceptance", "attempt")]
    [InlineData("acceptance", "mission")]
    [InlineData("acceptance", "job")]
    public void WrongSourceEvidenceCannotAdvanceOrMutateAttempt(string stage, string wrongField)
    {
        var attempt = CreatePlannedAttempt();
        var correlationId = Guid.NewGuid();
        var missionId = Guid.NewGuid();
        attempt.MarkPreflightReady("preflight", ActorId, "Coordinator", "Ready",
            correlationId, timestamp: Now.AddMinutes(1));
        attempt.StartFlight(missionId, Now.AddMinutes(2), "Coordinator", "Start",
            actorId: ActorId, correlationId: correlationId);
        attempt.RecordFlightCompleted(Now.AddMinutes(3), "flight-receipt", "Coordinator", "Landed",
            ActorId, correlationId);

        if (stage != "upload")
        {
            attempt.MarkUploaded(UploadEvidence(attempt, "manifest-A"), Now.AddMinutes(4),
                "Media", "Uploaded", ActorId, correlationId);
        }
        if (stage == "acceptance")
        {
            attempt.StartProcessing(InputEvidence(attempt, "approval-A", "job-A"), Now.AddMinutes(5),
                "Coordinator", "Processing", ActorId, correlationId);
        }

        Guid ContextId(string field, Guid expected) =>
            wrongField == field ? Guid.NewGuid() : expected;
        var tenantId = ContextId("tenant", attempt.TenantId);
        var farmId = ContextId("farm", attempt.FarmId);
        var orderId = ContextId("order", attempt.OrderId);
        var attemptId = ContextId("attempt", attempt.Id);
        var evidenceMissionId = ContextId("mission", missionId);
        var before = JsonSerializer.Serialize(attempt);

        var exception = Assert.Throws<SurveyDomainException>(() =>
        {
            switch (stage)
            {
                case "upload":
                    attempt.MarkUploaded(new CaptureUploadManifestEvidence(
                        tenantId, farmId, orderId, attemptId, evidenceMissionId,
                        "manifest-A", "test-manifest-sha256"), Now.AddMinutes(4),
                        "Media", "Uploaded", ActorId, correlationId);
                    break;
                case "processing":
                    attempt.StartProcessing(new CaptureInputApprovalEvidence(
                        tenantId, farmId, orderId, attemptId, evidenceMissionId,
                        wrongField == "manifest" ? "manifest-B" : "manifest-A",
                        wrongField == "fingerprint" ? "different-sha256" : "test-manifest-sha256",
                        "approval-A", "job-A"), Now.AddMinutes(5),
                        "Coordinator", "Processing", ActorId, correlationId);
                    break;
                case "acceptance":
                    attempt.AcceptProcessing(new CaptureProcessingAcceptanceEvidence(
                        tenantId, farmId, orderId, attemptId, evidenceMissionId,
                        wrongField == "job" ? "job-B" : "job-A", "acceptance-A"), Now.AddMinutes(6),
                        "Worker", "Accepted", ActorId, correlationId);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(stage));
            }
        });

        Assert.Equal(CaptureAttemptDomainErrorCodes.EvidenceIdentityMismatch, exception.Code);
        Assert.Equal(before, JsonSerializer.Serialize(attempt));
    }

    [Fact]
    public void IdempotencyEnvelopeAndReplaySemanticsAreStrictlyEnforced()
    {
        var attempt = CreatePlannedAttempt();
        var corrId = Guid.NewGuid();
        var op1 = Guid.NewGuid();
        var fp1 = "fingerprint-alpha";

        // 1. Missing fingerprint when operationId is provided throws ArgumentException
        Assert.Throws<ArgumentException>(() =>
            attempt.MarkPreflightReady("pf", ActorId, "C", "R", corrId, operationId: op1, payloadFingerprint: null));
        Assert.Throws<ArgumentException>(() =>
            attempt.MarkPreflightReady("pf", ActorId, "C", "R", corrId, operationId: op1, payloadFingerprint: "  "));

        // 2. Empty operationId throws ArgumentException
        Assert.Throws<ArgumentException>(() =>
            attempt.MarkPreflightReady("pf", ActorId, "C", "R", corrId, operationId: Guid.Empty, payloadFingerprint: fp1));

        // 3. Missing operationId when fingerprint is provided throws ArgumentException
        Assert.Throws<ArgumentException>(() =>
            attempt.MarkPreflightReady("pf", ActorId, "C", "R", corrId, operationId: null, payloadFingerprint: fp1));

        // 4. Successful initial execution with expectedVersion = 1
        attempt.MarkPreflightReady(
            "pf",
            ActorId,
            "C",
            "R",
            corrId,
            operationId: op1,
            payloadFingerprint: fp1,
            timestamp: Now.AddMinutes(2),
            expectedVersion: 1u);

        Assert.Equal(CaptureAttemptStatus.PreflightReady, attempt.Status);
        Assert.Equal(2u, attempt.Version);

        // 5. Retrying with original expectedVersion = 1 succeeds idempotently (does NOT throw VersionConflict!)
        attempt.MarkPreflightReady(
            "pf",
            ActorId,
            "C",
            "R",
            corrId,
            operationId: op1,
            payloadFingerprint: fp1,
            timestamp: Now.AddMinutes(2),
            expectedVersion: 1u);

        // 6. Progress attempt to next status (InFlight -> AwaitingUpload)
        var missionId = Guid.NewGuid();
        attempt.StartFlight(missionId, Now.AddMinutes(5), "C", "S", null, ActorId, corrId);
        attempt.RecordFlightCompleted(Now.AddMinutes(20), "rec", "C", "D", ActorId, corrId);
        Assert.Equal(CaptureAttemptStatus.AwaitingUpload, attempt.Status);

        // 7. Retrying the earlier MarkPreflightReady operation (op1) after attempt progressed still returns no-op (does NOT throw OperationConflict!)
        attempt.MarkPreflightReady(
            "pf",
            ActorId,
            "C",
            "R",
            corrId,
            operationId: op1,
            payloadFingerprint: fp1,
            timestamp: Now.AddMinutes(2),
            expectedVersion: 1u);

        // 8. Retrying with same operationId but different fingerprint throws OperationConflict
        var exDifferentFp = Assert.Throws<SurveyDomainException>(() =>
            attempt.MarkPreflightReady(
                "pf",
                ActorId,
                "C",
                "R",
                corrId,
                operationId: op1,
                payloadFingerprint: "fingerprint-beta"));
        Assert.Equal(CaptureAttemptDomainErrorCodes.OperationConflict, exDifferentFp.Code);

        // 9. Reusing operationId for a different action throws OperationConflict
        var exDifferentAction = Assert.Throws<SurveyDomainException>(() =>
            attempt.InvalidatePreflight(
                "Cancel pf",
                null,
                ActorId,
                "C",
                corrId,
                operationId: op1,
                payloadFingerprint: fp1));
        Assert.Equal(CaptureAttemptDomainErrorCodes.OperationConflict, exDifferentAction.Code);
    }

    [Fact]
    public void HistoryFactoryGuardsAgainstIllegalTransitionsAndEmptyIds()
    {
        var attemptId = Guid.NewGuid();
        var corrId = Guid.NewGuid();

        // 1. CreateTransition with sequence < 2 throws ArgumentOutOfRangeException
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CaptureAttemptHistory.CreateTransition(
                Guid.NewGuid(),
                attemptId,
                transitionSequence: 1u,
                action: CaptureAttemptAction.MarkPreflightReady,
                fromStatus: CaptureAttemptStatus.Planned,
                toStatus: CaptureAttemptStatus.PreflightReady,
                actorId: ActorId,
                source: "Coordinator",
                reason: "Ready",
                evidenceReference: null,
                correlationId: corrId,
                operationId: null,
                payloadFingerprint: null,
                predecessorHistoryId: Guid.NewGuid(),
                createdAt: Now));

        // 2. CreateTransition with illegal transition (Accepted -> Planned) throws InvalidTransition
        var exIllegal = Assert.Throws<SurveyDomainException>(() =>
            CaptureAttemptHistory.CreateTransition(
                Guid.NewGuid(),
                attemptId,
                transitionSequence: 2u,
                action: CaptureAttemptAction.InvalidatePreflight,
                fromStatus: CaptureAttemptStatus.Accepted,
                toStatus: CaptureAttemptStatus.Planned,
                actorId: ActorId,
                source: "Coordinator",
                reason: "Illegal reset",
                evidenceReference: null,
                correlationId: corrId,
                operationId: null,
                payloadFingerprint: null,
                predecessorHistoryId: Guid.NewGuid(),
                createdAt: Now));
        Assert.Equal(CaptureAttemptDomainErrorCodes.InvalidTransition, exIllegal.Code);

        // 3. Actor ID cannot be Guid.Empty
        Assert.Throws<ArgumentException>(() =>
            CaptureAttemptHistory.CreateInitial(
                Guid.NewGuid(),
                attemptId,
                action: CaptureAttemptAction.CreateInitial,
                actorId: Guid.Empty,
                source: "Coordinator",
                reason: "Initial",
                evidenceReference: null,
                correlationId: corrId,
                operationId: null,
                payloadFingerprint: null,
                createdAt: Now));

        // 4. Operation ID cannot be Guid.Empty
        Assert.Throws<ArgumentException>(() =>
            CaptureAttemptHistory.CreateInitial(
                Guid.NewGuid(),
                attemptId,
                action: CaptureAttemptAction.CreateInitial,
                actorId: ActorId,
                source: "Coordinator",
                reason: "Initial",
                evidenceReference: null,
                correlationId: corrId,
                operationId: Guid.Empty,
                payloadFingerprint: "fp",
                createdAt: Now));

        // 5. Incomplete idempotency envelope throws ArgumentException
        Assert.Throws<ArgumentException>(() =>
            CaptureAttemptHistory.CreateInitial(
                Guid.NewGuid(),
                attemptId,
                action: CaptureAttemptAction.CreateInitial,
                actorId: ActorId,
                source: "Coordinator",
                reason: "Initial",
                evidenceReference: null,
                correlationId: corrId,
                operationId: Guid.NewGuid(),
                payloadFingerprint: null,
                createdAt: Now));
    }

    [Fact]
    public void StartFlightInvariantsEnforcedStrictly()
    {
        var attempt = CreatePlannedAttempt();
        var corrId = Guid.NewGuid();

        // 1. Cannot start without being PreflightReady
        var exNotReady = Assert.Throws<SurveyDomainException>(() =>
            attempt.StartFlight(Guid.NewGuid(), Now.AddMinutes(5), "Coord", "Start", null, ActorId, corrId));
        Assert.Equal(CaptureAttemptDomainErrorCodes.InvalidTransition, exNotReady.Code);

        attempt.MarkPreflightReady("pf-ok", ActorId, "Insp", "Ready", corrId, timestamp: Now.AddMinutes(1));

        // 2. Bound Mission ID cannot be empty
        var exEmptyMission = Assert.Throws<SurveyDomainException>(() =>
            attempt.StartFlight(Guid.Empty, Now.AddMinutes(5), "Coord", "Start", null, ActorId, corrId));
        Assert.Equal(CaptureAttemptDomainErrorCodes.BoundMissionRequired, exEmptyMission.Code);

        // 3. Bound Mission mismatch
        var boundMissionId = Guid.NewGuid();
        attempt.BindMission(boundMissionId);

        var exMismatch = Assert.Throws<SurveyDomainException>(() =>
            attempt.StartFlight(Guid.NewGuid(), Now.AddMinutes(5), "Coord", "Start", null, ActorId, corrId));
        Assert.Equal(CaptureAttemptDomainErrorCodes.BoundMissionRequired, exMismatch.Code);

        // 4. StartedAt cannot be before CreatedAt
        var exEarlier = Assert.Throws<ArgumentException>(() =>
            attempt.StartFlight(boundMissionId, Now.AddMinutes(-5), "Coord", "Start", null, ActorId, corrId));
        Assert.Contains("earlier", exEarlier.Message);
    }

    [Fact]
    public void FlightCompletionInvariantsEnforcedStrictly()
    {
        var attempt = CreatePlannedAttempt();
        var missionId = Guid.NewGuid();
        var corrId = Guid.NewGuid();

        attempt.MarkPreflightReady("pf-ok", ActorId, "Insp", "Ready", corrId, timestamp: Now.AddMinutes(1));
        attempt.StartFlight(missionId, Now.AddMinutes(10), "Coord", "Start", null, ActorId, corrId);

        // 1. EndedAt cannot be before StartedAt
        var exEndedBeforeStarted = Assert.Throws<ArgumentException>(() =>
            attempt.RecordFlightCompleted(Now.AddMinutes(5), "receipt-ok", "Monitor", "Touchdown", ActorId, corrId));
        Assert.Contains("earlier", exEndedBeforeStarted.Message);

        // 2. Missing flight receipt reference
        var exMissingReceipt = Assert.Throws<SurveyDomainException>(() =>
            attempt.RecordFlightCompleted(Now.AddMinutes(20), "", "Monitor", "Touchdown", ActorId, corrId));
        Assert.Equal(CaptureAttemptDomainErrorCodes.FlightCompletionEvidenceRequired, exMissingReceipt.Code);
    }

    [Fact]
    public void UploadAndProcessingInvariantsEnforcedStrictly()
    {
        var attempt = CreatePlannedAttempt();
        var missionId = Guid.NewGuid();
        var corrId = Guid.NewGuid();

        attempt.MarkPreflightReady("pf-ok", ActorId, "Insp", "Ready", corrId, timestamp: Now.AddMinutes(1));
        attempt.StartFlight(missionId, Now.AddMinutes(5), "Coord", "Start", null, ActorId, corrId);
        attempt.RecordFlightCompleted(Now.AddMinutes(25), "receipt-ok", "Monitor", "Landed", ActorId, corrId);

        // 1. Missing upload manifest fails
        var exMissingManifest = Assert.Throws<ArgumentException>(() =>
            attempt.MarkUploaded(UploadEvidence(attempt, "  "), Now.AddMinutes(30), "Media", "Uploaded", ActorId, corrId));
        Assert.Equal("manifestId", exMissingManifest.ParamName);

        attempt.MarkUploaded(UploadEvidence(attempt, "manifest-123"), Now.AddMinutes(30), "Media", "Uploaded", ActorId, corrId);

        // 2. Missing InputApproval reference fails
        var exMissingApproval = Assert.Throws<ArgumentException>(() =>
            attempt.StartProcessing(InputEvidence(attempt, "", "job-1"), Now.AddMinutes(35), "Coord", "Proc", ActorId, corrId));
        Assert.Equal("inputApprovalId", exMissingApproval.ParamName);

        // 3. Missing Job reference fails
        var exMissingJob = Assert.Throws<ArgumentException>(() =>
            attempt.StartProcessing(InputEvidence(attempt, "approval-1", ""), Now.AddMinutes(35), "Coord", "Proc", ActorId, corrId));
        Assert.Equal("processingJobId", exMissingJob.ParamName);

        attempt.StartProcessing(InputEvidence(attempt, "approval-1", "job-1"), Now.AddMinutes(35), "Coord", "Proc", ActorId, corrId);

        // 4. Input approval does NOT equal Accepted (status is Processing)
        Assert.Equal(CaptureAttemptStatus.Processing, attempt.Status);
        Assert.False(attempt.IsTerminal);

        // 5. Missing Acceptance receipt fails
        var exMissingAcceptance = Assert.Throws<ArgumentException>(() =>
            attempt.AcceptProcessing(AcceptanceEvidence(attempt, ""), Now.AddMinutes(50), "AI", "Done", ActorId, corrId));
        Assert.Equal("acceptanceReceiptId", exMissingAcceptance.ParamName);
    }

    [Fact]
    public void ReflightCannotBeDeclaredWhileAirborneMustWaitUntilFlightEnds()
    {
        var attempt = CreatePlannedAttempt();
        var missionId = Guid.NewGuid();
        var corrId = Guid.NewGuid();
        var approverId = Guid.NewGuid();

        attempt.MarkPreflightReady("pf-ok", ActorId, "Insp", "Ready", corrId, timestamp: Now.AddMinutes(1));
        attempt.StartFlight(missionId, Now.AddMinutes(5), "Coord", "Start", null, ActorId, corrId);

        // While InFlight (airborne), declaring re-flight is forbidden
        var exAirborne = Assert.Throws<SurveyDomainException>(() =>
            attempt.RequireReflight(
                "camera-sensor-error",
                "Sensor reported bad calibration mid-flight",
                approverId,
                Now.AddMinutes(10),
                "Telemetry",
                corrId));
        Assert.Equal(CaptureAttemptDomainErrorCodes.InvalidTransition, exAirborne.Code);

        // Flight completes first
        attempt.RecordFlightCompleted(Now.AddMinutes(20), "receipt-landing", "Telemetry", "Touchdown", ActorId, corrId);

        // Now post-flight re-flight declaration succeeds
        attempt.RequireReflight(
            "camera-sensor-error",
            "Sensor reported bad calibration mid-flight",
            approverId,
            Now.AddMinutes(25),
            "QualityInspector",
            corrId);

        Assert.Equal(CaptureAttemptStatus.ReflightRequired, attempt.Status);
        Assert.True(attempt.IsTerminal);
    }

    [Fact]
    public void CancellationAllowedBeforeExecutionForbiddenAfterExecution()
    {
        // 1. Cancellation from Planned succeeds
        var plannedAttempt = CreatePlannedAttempt();
        var corrId = Guid.NewGuid();

        plannedAttempt.Cancel("Customer requested appointment cancellation", "refund-ticket-1", ActorId, "Admin", corrId, timestamp: Now.AddMinutes(5));
        Assert.Equal(CaptureAttemptStatus.Cancelled, plannedAttempt.Status);
        Assert.True(plannedAttempt.IsTerminal);
        Assert.Equal("Customer requested appointment cancellation", plannedAttempt.CancellationReason);
        Assert.Equal("refund-ticket-1", plannedAttempt.CancellationEvidenceReference);

        // 2. Cancellation from PreflightReady succeeds
        var preflightAttempt = CreatePlannedAttempt();
        preflightAttempt.MarkPreflightReady("pf-evidence", ActorId, "Insp", "Ready", corrId, timestamp: Now.AddMinutes(2));
        preflightAttempt.Cancel("Weather hazard approaching, mission aborted prior to takeoff", null, ActorId, "Manager", corrId, timestamp: Now.AddMinutes(4));
        Assert.Equal(CaptureAttemptStatus.Cancelled, preflightAttempt.Status);
        Assert.True(preflightAttempt.IsTerminal);

        // 3. Cancellation while InFlight is forbidden (cannot cancel execution attempt)
        var flyingAttempt = CreatePlannedAttempt();
        flyingAttempt.MarkPreflightReady("pf-evidence", ActorId, "Insp", "Ready", corrId, timestamp: Now.AddMinutes(1));
        flyingAttempt.StartFlight(Guid.NewGuid(), Now.AddMinutes(5), "Coord", "Start", null, ActorId, corrId);

        var exFlight = Assert.Throws<SurveyDomainException>(() =>
            flyingAttempt.Cancel("Cancel mid-flight", null, ActorId, "Manager", corrId, timestamp: Now.AddMinutes(10)));
        Assert.Equal(CaptureAttemptDomainErrorCodes.CancellationNotAllowed, exFlight.Code);

        // 4. Cancellation after landing is forbidden
        flyingAttempt.RecordFlightCompleted(Now.AddMinutes(20), "receipt", "Coord", "Done", ActorId, corrId);
        var exLanded = Assert.Throws<SurveyDomainException>(() =>
            flyingAttempt.Cancel("Cancel after landed", null, ActorId, "Manager", corrId, timestamp: Now.AddMinutes(25)));
        Assert.Equal(CaptureAttemptDomainErrorCodes.CancellationNotAllowed, exLanded.Code);
    }

    [Fact]
    public void TerminalAttemptIsFrozenNoTransitionsOrRebindingAllowed()
    {
        var attempt = CreatePlannedAttempt();
        var corrId = Guid.NewGuid();

        attempt.Cancel("Pre-execution cancellation", null, ActorId, "Admin", corrId, timestamp: Now.AddMinutes(2));
        Assert.True(attempt.IsTerminal);

        // Attempting any transition throws AttemptTerminal
        var ex1 = Assert.Throws<SurveyDomainException>(() =>
            attempt.MarkPreflightReady("pf", ActorId, "Coord", "Try", corrId));
        Assert.Equal(CaptureAttemptDomainErrorCodes.AttemptTerminal, ex1.Code);

        var ex2 = Assert.Throws<SurveyDomainException>(() =>
            attempt.BindMission(Guid.NewGuid()));
        Assert.Equal(CaptureAttemptDomainErrorCodes.AttemptTerminal, ex2.Code);

        var ex3 = Assert.Throws<SurveyDomainException>(() =>
            attempt.Cancel("Double cancel", null, ActorId, "Admin", corrId));
        Assert.Equal(CaptureAttemptDomainErrorCodes.AttemptTerminal, ex3.Code);
    }

    [Fact]
    public void EnsureVersionValidatesConcurrencyToken()
    {
        var attempt = CreatePlannedAttempt();
        Assert.Equal(1u, attempt.Version);

        // Matching version passes
        attempt.EnsureVersion(1u);

        // Mismatched version throws VersionConflict
        var ex = Assert.Throws<SurveyDomainException>(() => attempt.EnsureVersion(2u));
        Assert.Equal(CaptureAttemptDomainErrorCodes.VersionConflict, ex.Code);
    }

    [Theory]
    [InlineData(CaptureAttemptStatus.Planned, CaptureAttemptStatus.PreflightReady, true)]
    [InlineData(CaptureAttemptStatus.Planned, CaptureAttemptStatus.Cancelled, true)]
    [InlineData(CaptureAttemptStatus.Planned, CaptureAttemptStatus.InFlight, false)]
    [InlineData(CaptureAttemptStatus.Planned, CaptureAttemptStatus.Uploaded, false)]
    [InlineData(CaptureAttemptStatus.PreflightReady, CaptureAttemptStatus.Planned, true)]
    [InlineData(CaptureAttemptStatus.PreflightReady, CaptureAttemptStatus.InFlight, true)]
    [InlineData(CaptureAttemptStatus.PreflightReady, CaptureAttemptStatus.Cancelled, true)]
    [InlineData(CaptureAttemptStatus.PreflightReady, CaptureAttemptStatus.Processing, false)]
    [InlineData(CaptureAttemptStatus.InFlight, CaptureAttemptStatus.AwaitingUpload, true)]
    [InlineData(CaptureAttemptStatus.InFlight, CaptureAttemptStatus.FlightFailed, true)]
    [InlineData(CaptureAttemptStatus.InFlight, CaptureAttemptStatus.ReflightRequired, false)]
    [InlineData(CaptureAttemptStatus.InFlight, CaptureAttemptStatus.Cancelled, false)]
    [InlineData(CaptureAttemptStatus.AwaitingUpload, CaptureAttemptStatus.Uploaded, true)]
    [InlineData(CaptureAttemptStatus.AwaitingUpload, CaptureAttemptStatus.ReflightRequired, true)]
    [InlineData(CaptureAttemptStatus.AwaitingUpload, CaptureAttemptStatus.Cancelled, false)]
    [InlineData(CaptureAttemptStatus.Uploaded, CaptureAttemptStatus.Processing, true)]
    [InlineData(CaptureAttemptStatus.Uploaded, CaptureAttemptStatus.ReflightRequired, true)]
    [InlineData(CaptureAttemptStatus.Uploaded, CaptureAttemptStatus.Accepted, false)]
    [InlineData(CaptureAttemptStatus.Processing, CaptureAttemptStatus.Accepted, true)]
    [InlineData(CaptureAttemptStatus.Processing, CaptureAttemptStatus.ReflightRequired, true)]
    [InlineData(CaptureAttemptStatus.Processing, CaptureAttemptStatus.Cancelled, false)]
    [InlineData(CaptureAttemptStatus.Accepted, CaptureAttemptStatus.Planned, false)]
    [InlineData(CaptureAttemptStatus.Accepted, CaptureAttemptStatus.ReflightRequired, false)]
    [InlineData(CaptureAttemptStatus.FlightFailed, CaptureAttemptStatus.Planned, false)]
    [InlineData(CaptureAttemptStatus.Cancelled, CaptureAttemptStatus.Planned, false)]
    public void CanTransitionTableExplicitlyVerified(
        CaptureAttemptStatus from,
        CaptureAttemptStatus to,
        bool expected)
    {
        Assert.Equal(expected, CaptureAttempt.CanTransition(from, to));
    }

    [Theory]
    [InlineData(CaptureAttemptStatus.Planned, false)]
    [InlineData(CaptureAttemptStatus.PreflightReady, false)]
    [InlineData(CaptureAttemptStatus.InFlight, false)]
    [InlineData(CaptureAttemptStatus.AwaitingUpload, false)]
    [InlineData(CaptureAttemptStatus.Uploaded, false)]
    [InlineData(CaptureAttemptStatus.Processing, false)]
    [InlineData(CaptureAttemptStatus.Accepted, true)]
    [InlineData(CaptureAttemptStatus.ReflightRequired, true)]
    [InlineData(CaptureAttemptStatus.FlightFailed, true)]
    [InlineData(CaptureAttemptStatus.Cancelled, true)]
    public void IsTerminalStatusExplicitlyVerified(
        CaptureAttemptStatus status,
        bool expected)
    {
        Assert.Equal(expected, CaptureAttempt.IsTerminalStatus(status));
    }
}
