namespace AgriDrone.IntegrationContracts.Surveys;

public enum CaptureCancellationDecisionKind
{
    CustomerBeforeCapture,
    CustomerAfterAcceptableCapture,
    OperationalPostponement,
    ServiceFailure
}

public record PrepareCaptureAttemptRequest(
    Guid OrderId,
    uint ExpectedOrderVersion,
    Guid ActorId,
    Guid? DroneId,
    string? Reason,
    Guid CorrelationId,
    Guid OperationId,
    string PayloadFingerprint);

public record PrepareCaptureAttemptResult(
    bool Success,
    Guid AttemptId,
    int AttemptIndex,
    Guid MissionId,
    string Status,
    uint Version,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record ScheduleDiseaseCaptureRequest(
    Guid OrderId,
    uint ExpectedOrderVersion,
    Guid ActorId,
    Guid DroneId,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    Guid CorrelationId,
    Guid OperationId);

public record RescheduleDiseaseCaptureRequest(
    Guid OrderId,
    Guid AttemptId,
    uint ExpectedOrderVersion,
    Guid ActorId,
    Guid DroneId,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    Guid CorrelationId);

public record StartCaptureFlightRequest(
    Guid OrderId,
    uint ExpectedOrderVersion,
    Guid AttemptId,
    uint ExpectedAttemptVersion,
    Guid MissionId,
    Guid ActorId,
    Guid CorrelationId,
    Guid OperationId,
    string PayloadFingerprint);

public record StartCaptureFlightResult(
    bool Success,
    Guid AttemptId,
    string Status,
    uint Version,
    DateTimeOffset StartedAt,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record EndCaptureFlightRequest(
    Guid OrderId,
    uint ExpectedOrderVersion,
    Guid AttemptId,
    uint ExpectedAttemptVersion,
    Guid ActorId,
    Guid CorrelationId,
    DateTimeOffset EndedAt,
    string FlightCompletedReceipt,
    Guid OperationId,
    string PayloadFingerprint);

public record EndCaptureFlightResult(
    bool Success,
    Guid AttemptId,
    string Status,
    uint Version,
    DateTimeOffset EndedAt,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record RecordFlightFailureRequest(
    Guid OrderId,
    uint ExpectedOrderVersion,
    Guid AttemptId,
    uint ExpectedAttemptVersion,
    Guid ActorId,
    Guid CorrelationId,
    DateTimeOffset FailedAt,
    string FailureReason,
    string FailureIncidentEvidence,
    Guid OperationId,
    string PayloadFingerprint);

public record RecordFlightFailureResult(
    bool Success,
    Guid AttemptId,
    string Status,
    uint Version,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record FreezeCaptureInputManifestRequest(
    Guid OrderId,
    uint ExpectedOrderVersion,
    Guid AttemptId,
    uint ExpectedAttemptVersion,
    Guid MissionId,
    Guid ActorId,
    Guid CorrelationId,
    Guid OperationId,
    string PayloadFingerprint);

public record FreezeCaptureInputManifestResult(
    bool Success,
    Guid AttemptId,
    Guid ManifestId,
    string ManifestFingerprint,
    int TotalFileCount,
    long TotalSizeBytes,
    string Status,
    uint Version,
    string? ErrorCode = null,
    string? ErrorMessage = null,
    Guid? ProcessingJobId = null,
    uint? OrderVersion = null);

public record ReopenCaptureInputManifestRequest(
    Guid OrderId,
    uint ExpectedOrderVersion,
    Guid AttemptId,
    uint ExpectedAttemptVersion,
    Guid ManifestId,
    Guid ActorId,
    Guid CorrelationId,
    Guid OperationId,
    string PayloadFingerprint,
    string Reason,
    bool ReplaceApprovedInput = false);

public record ReopenCaptureInputManifestResult(
    bool Success,
    Guid AttemptId,
    Guid MissionId,
    Guid SupersededManifestId,
    string Status,
    uint Version,
    uint OrderVersion,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record ApproveCaptureInputRequest(
    Guid OrderId,
    uint ExpectedOrderVersion,
    Guid AttemptId,
    uint ExpectedAttemptVersion,
    Guid ManifestId,
    string ManifestFingerprint,
    Guid ProcessingJobId,
    Guid ActorId,
    Guid CorrelationId,
    Guid OperationId,
    string PayloadFingerprint,
    string? Notes = null);

public record ApproveCaptureInputResult(
    bool Success,
    Guid AttemptId,
    Guid ApprovalId,
    string Status,
    uint Version,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record RecoverCaptureMissionRequest(
    Guid OrderId,
    Guid AttemptId,
    Guid ActorId,
    Guid CorrelationId);

public record RecoverCaptureMissionResult(
    bool Success,
    Guid AttemptId,
    Guid MissionId,
    Guid ApprovalId,
    Guid ManifestId,
    Guid ProcessingJobId,
    uint MissionVersion,
    bool AlreadyReady,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record CreateQualityReflightRequest(
    Guid OrderId,
    uint ExpectedOrderVersion,
    Guid ParentAttemptId,
    uint ExpectedParentAttemptVersion,
    Guid DroneId,
    DateTimeOffset ProposedStartAt,
    DateTimeOffset ProposedEndAt,
    Guid ActorId,
    Guid CorrelationId,
    string Reason,
    string TechnicalQualityEvidence,
    Guid OperationId,
    string PayloadFingerprint);

public record CreateQualityReflightResult(
    bool Success,
    Guid SuccessorAttemptId,
    int SuccessorAttemptIndex,
    Guid NewMissionId,
    string Status,
    uint Version,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record CancelCaptureOrderRequest(
    Guid OrderId,
    uint ExpectedOrderVersion,
    Guid ActorId,
    Guid RequesterId,
    CaptureCancellationDecisionKind DecisionKind,
    decimal RequestedRetentionAmount,
    Guid? InputApprovalEvidenceId,
    string ReasonCode,
    string ReasonDescription,
    Guid CorrelationId,
    Guid OperationId,
    string PayloadFingerprint);

public record CancelCaptureOrderResult(
    bool Success,
    Guid OrderId,
    string Status,
    decimal RefundAmount,
    decimal RetainedAmount,
    Guid? CancellationDecisionId = null,
    Guid? RefundObligationId = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record RecordCaptureNoShowRequest(
    Guid OrderId,
    uint ExpectedOrderVersion,
    Guid AppointmentId,
    uint ExpectedAppointmentVersion,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    string Reason,
    string EvidenceReference,
    Guid CorrelationId,
    Guid OperationId,
    string PayloadFingerprint);

public record RecordCaptureNoShowResult(
    bool Success,
    Guid OrderId,
    Guid AppointmentId,
    Guid NoShowId,
    int OccurrenceIndex,
    string Disposition,
    string OrderStatus,
    string AppointmentStatus,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public interface ISurveyCaptureExecutionCoordinator
{
    Task<CaptureDroneSelection?> GetDroneSelectionAsync(
        Guid orderId, Guid actorId, DateTimeOffset? startAt, DateTimeOffset? endAt,
        CancellationToken cancellationToken = default) => Task.FromResult<CaptureDroneSelection?>(null);

    Task<PrepareCaptureAttemptResult> PrepareAttemptAsync(
        PrepareCaptureAttemptRequest request,
        CancellationToken cancellationToken = default);

    Task<PrepareCaptureAttemptResult> ScheduleDiseaseCaptureAsync(
        ScheduleDiseaseCaptureRequest request,
        CancellationToken cancellationToken = default);

    Task<PrepareCaptureAttemptResult> RescheduleDiseaseCaptureAsync(
        RescheduleDiseaseCaptureRequest request,
        CancellationToken cancellationToken = default);

    Task<StartCaptureFlightResult> StartFlightAsync(
        StartCaptureFlightRequest request,
        CancellationToken cancellationToken = default);

    Task<EndCaptureFlightResult> EndFlightAsync(
        EndCaptureFlightRequest request,
        CancellationToken cancellationToken = default);

    Task<RecordFlightFailureResult> RecordFlightFailureAsync(
        RecordFlightFailureRequest request,
        CancellationToken cancellationToken = default);

    Task<FreezeCaptureInputManifestResult> FreezeInputManifestAsync(
        FreezeCaptureInputManifestRequest request,
        CancellationToken cancellationToken = default);

    Task<ReopenCaptureInputManifestResult> ReopenInputManifestAsync(
        ReopenCaptureInputManifestRequest request,
        CancellationToken cancellationToken = default);

    Task<ApproveCaptureInputResult> ApproveInputAsync(
        ApproveCaptureInputRequest request,
        CancellationToken cancellationToken = default);

    Task<RecoverCaptureMissionResult> RecoverApprovedMissionAsync(
        RecoverCaptureMissionRequest request,
        CancellationToken cancellationToken = default);

    Task<CreateQualityReflightResult> CreateQualityReflightAsync(
        CreateQualityReflightRequest request,
        CancellationToken cancellationToken = default);

    Task<CancelCaptureOrderResult> CancelOrderAsync(
        CancelCaptureOrderRequest request,
        CancellationToken cancellationToken = default);

    Task<RecordCaptureNoShowResult> RecordNoShowAsync(
        RecordCaptureNoShowRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record CaptureDroneSelection(
    Guid FarmId, DateTimeOffset StartAt, DateTimeOffset EndAt, bool MapsPoles, bool ScreensDisease);
