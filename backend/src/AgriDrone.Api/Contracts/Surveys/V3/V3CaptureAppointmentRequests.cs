using System.ComponentModel.DataAnnotations;
using AgriDrone.IntegrationContracts.Surveys;

namespace AgriDrone.Api.Contracts.Surveys.V3;

public sealed record ProposeV3CaptureAppointmentRequest(
    [param: Required] DateOnly? ScheduledDate,
    [param: Required]
    [param: Range(typeof(uint), "1", "4294967295")]
    uint? ExpectedOrderVersion,
    Guid? ExpectedCurrentAppointmentId = null,
    uint? ExpectedCurrentAppointmentVersion = null);

public sealed record ConfirmV3CaptureAppointmentRequest(
    [param: Required]
    [param: Range(typeof(uint), "1", "4294967295")]
    uint? ExpectedOrderVersion,
    [param: Required]
    [param: Range(typeof(uint), "1", "4294967295")]
    uint? ExpectedAppointmentVersion);

public sealed record RequestV3CaptureAppointmentRescheduleRequest(
    [param: Required]
    [param: StringLength(1000, MinimumLength = 1)]
    string Reason,
    [param: Required]
    [param: Range(typeof(uint), "1", "4294967295")]
    uint? ExpectedOrderVersion,
    [param: Required]
    [param: Range(typeof(uint), "1", "4294967295")]
    uint? ExpectedAppointmentVersion);

public sealed record InitiateV3OrderDepositRequest(
    [param: Required]
    [param: Range(typeof(uint), "1", "4294967295")]
    uint? ExpectedOrderVersion);

public sealed record PrepareV3CaptureAttemptRequest(
    [param: Required] Guid? DroneId,
    [param: Required] uint? ExpectedOrderVersion,
    [param: Required] Guid? OperationId,
    [param: Required, StringLength(128, MinimumLength = 1)] string PayloadFingerprint,
    [param: StringLength(500)] string? Reason = null);

public sealed record StartV3CaptureFlightRequest(
    [param: Required] Guid? MissionId,
    [param: Required] uint? ExpectedOrderVersion,
    [param: Required] uint? ExpectedAttemptVersion,
    [param: Required] Guid? OperationId,
    [param: Required, StringLength(128, MinimumLength = 1)] string PayloadFingerprint);

public sealed record RecordV3CaptureNoShowRequest(
    [param: Required] Guid? AppointmentId,
    [param: Required] uint? ExpectedOrderVersion,
    [param: Required] uint? ExpectedAppointmentVersion,
    [param: Required] DateTimeOffset? OccurredAt,
    [param: Required, StringLength(500, MinimumLength = 1)] string Reason,
    [param: Required, StringLength(500, MinimumLength = 1)] string EvidenceReference,
    [param: Required] Guid? OperationId,
    [param: Required, StringLength(128, MinimumLength = 1)] string PayloadFingerprint);

public sealed record EndV3CaptureFlightRequest(
    [param: Required] uint? ExpectedOrderVersion,
    [param: Required] uint? ExpectedAttemptVersion,
    [param: Required] DateTimeOffset? EndedAt,
    [param: Required, StringLength(500, MinimumLength = 1)] string FlightCompletedReceipt,
    [param: Required] Guid? OperationId,
    [param: Required, StringLength(128, MinimumLength = 1)] string PayloadFingerprint);

public sealed record FailV3CaptureFlightRequest(
    [param: Required] uint? ExpectedOrderVersion,
    [param: Required] uint? ExpectedAttemptVersion,
    [param: Required] DateTimeOffset? FailedAt,
    [param: Required, StringLength(500, MinimumLength = 1)] string FailureReason,
    [param: Required, StringLength(500, MinimumLength = 1)] string FailureIncidentEvidence,
    [param: Required] Guid? OperationId,
    [param: Required, StringLength(128, MinimumLength = 1)] string PayloadFingerprint);

public sealed record FreezeV3CaptureManifestRequest(
    [param: Required] Guid? MissionId,
    [param: Required] uint? ExpectedOrderVersion,
    [param: Required] uint? ExpectedAttemptVersion,
    [param: Required] Guid? OperationId,
    [param: Required, StringLength(128, MinimumLength = 1)] string PayloadFingerprint);

public sealed record ReopenV3CaptureManifestRequest(
    [param: Required] Guid? ManifestId,
    [param: Required] uint? ExpectedOrderVersion,
    [param: Required] uint? ExpectedAttemptVersion,
    [param: Required] Guid? OperationId,
    [param: Required, StringLength(128, MinimumLength = 1)] string PayloadFingerprint,
    [param: Required, StringLength(500, MinimumLength = 1)] string Reason);

public sealed record ApproveV3CaptureInputRequest(
    [param: Required] Guid? ManifestId,
    [param: Required, StringLength(128, MinimumLength = 1)] string ManifestFingerprint,
    [param: Required] Guid? ProcessingJobId,
    [param: Required] uint? ExpectedOrderVersion,
    [param: Required] uint? ExpectedAttemptVersion,
    [param: Required] Guid? OperationId,
    [param: Required, StringLength(128, MinimumLength = 1)] string PayloadFingerprint,
    [param: StringLength(500)] string? Notes = null);

public sealed record CreateV3QualityReflightRequest(
    [param: Required] Guid? DroneId,
    [param: Required] DateOnly? ScheduledDate,
    [param: Required] uint? ExpectedOrderVersion,
    [param: Required] uint? ExpectedParentAttemptVersion,
    [param: Required, StringLength(500, MinimumLength = 1)] string Reason,
    [param: Required, StringLength(500, MinimumLength = 1)] string TechnicalQualityEvidence,
    [param: Required] Guid? OperationId,
    [param: Required, StringLength(128, MinimumLength = 1)] string PayloadFingerprint);

public sealed record CancelV3OwnerOrderRequest(
    [param: Required] uint? ExpectedOrderVersion,
    [param: Required, StringLength(64, MinimumLength = 1)] string ReasonCode,
    [param: Required, StringLength(500, MinimumLength = 1)] string ReasonDescription,
    [param: Required] Guid? OperationId,
    [param: Required, StringLength(128, MinimumLength = 1)] string PayloadFingerprint);

public sealed record DecideV3OrderCancellationRequest(
    [param: Required] Guid? RequesterId,
    [param: Required] CaptureCancellationDecisionKind? DecisionKind,
    [param: Range(typeof(decimal), "0", "79228162514264337593543950335")]
    decimal RequestedRetentionAmount,
    Guid? InputApprovalEvidenceId,
    [param: Required, StringLength(64, MinimumLength = 1)] string ReasonCode,
    [param: Required, StringLength(500, MinimumLength = 1)] string ReasonDescription,
    [param: Required] uint? ExpectedOrderVersion,
    [param: Required] Guid? OperationId,
    [param: Required, StringLength(128, MinimumLength = 1)] string PayloadFingerprint);
