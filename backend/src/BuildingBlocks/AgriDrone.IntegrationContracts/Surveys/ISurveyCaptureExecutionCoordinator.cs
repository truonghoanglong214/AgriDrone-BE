namespace AgriDrone.IntegrationContracts.Surveys;

public record PrepareCaptureAttemptRequest(
    Guid OrderId,
    uint ExpectedOrderVersion,
    Guid ActorId,
    Guid? DroneId,
    string? Reason,
    Guid CorrelationId,
    Guid? OperationId = null,
    string? PayloadFingerprint = null);

public record PrepareCaptureAttemptResult(
    bool Success,
    Guid AttemptId,
    int AttemptIndex,
    Guid MissionId,
    string Status,
    uint Version,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record StartCaptureFlightRequest(
    Guid OrderId,
    uint ExpectedOrderVersion,
    Guid AttemptId,
    uint ExpectedAttemptVersion,
    Guid MissionId,
    Guid ActorId,
    Guid CorrelationId,
    DateTimeOffset StartedAt,
    Guid? OperationId = null,
    string? PayloadFingerprint = null);

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
    Guid? OperationId = null,
    string? PayloadFingerprint = null);

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
    Guid? OperationId = null,
    string? PayloadFingerprint = null);

public record RecordFlightFailureResult(
    bool Success,
    Guid AttemptId,
    string Status,
    uint Version,
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
    DateTimeOffset ApprovedAt,
    string? Notes = null,
    Guid? OperationId = null,
    string? PayloadFingerprint = null);

public record ApproveCaptureInputResult(
    bool Success,
    Guid AttemptId,
    Guid ApprovalId,
    string Status,
    uint Version,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public record CreateQualityReflightRequest(
    Guid OrderId,
    uint ExpectedOrderVersion,
    Guid ParentAttemptId,
    uint ExpectedParentAttemptVersion,
    Guid NewMissionId,
    Guid ActorId,
    Guid CorrelationId,
    string Reason,
    string TechnicalQualityEvidence,
    DateTimeOffset CreatedAt,
    Guid? OperationId = null,
    string? PayloadFingerprint = null);

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
    string Reason,
    Guid CorrelationId,
    DateTimeOffset CancelledAt,
    Guid? OperationId = null,
    string? PayloadFingerprint = null);

public record CancelCaptureOrderResult(
    bool Success,
    Guid OrderId,
    string Status,
    decimal RefundAmount,
    decimal RetainedAmount,
    Guid? RefundObligationId = null,
    string? ErrorCode = null,
    string? ErrorMessage = null);

public interface ISurveyCaptureExecutionCoordinator
{
    Task<PrepareCaptureAttemptResult> PrepareAttemptAsync(
        PrepareCaptureAttemptRequest request,
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

    Task<ApproveCaptureInputResult> ApproveInputAsync(
        ApproveCaptureInputRequest request,
        CancellationToken cancellationToken = default);

    Task<CreateQualityReflightResult> CreateQualityReflightAsync(
        CreateQualityReflightRequest request,
        CancellationToken cancellationToken = default);

    Task<CancelCaptureOrderResult> CancelOrderAsync(
        CancelCaptureOrderRequest request,
        CancellationToken cancellationToken = default);
}
