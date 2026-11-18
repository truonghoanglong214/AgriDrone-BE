using System.ComponentModel.DataAnnotations;

namespace AgriDrone.Api.Contracts.Surveys.V3;

public sealed record CreateSurveyServiceV3ApiRequest(
    [param: Required, StringLength(50, MinimumLength = 1)]
    string Code,
    [param: Required, StringLength(150, MinimumLength = 1)]
    string Name,
    [param: Required, StringLength(4000, MinimumLength = 1)]
    string Description,
    bool ActivateImmediately = true);

public sealed record PublishSurveyServiceRevisionApiRequest(
    [param: Required]
    Guid SurveyServiceId,
    [param: Required, StringLength(200, MinimumLength = 1)]
    string Name,
    [param: Required, StringLength(2_000, MinimumLength = 1)]
    string Description,
    bool MapsPoles,
    bool ScreensDisease,
    int? ExpectedLatestVersionNumber,
    [param: Required, StringLength(100, MinimumLength = 1)]
    string ReasonCode,
    [param: StringLength(500)]
    string? EvidenceReference);

public sealed record PublishSurveyServicePriceV3ApiRequest(
    [param: Required]
    Guid SurveyServiceId,
    [param: Range(typeof(decimal), "1", "1000000000")]
    decimal PricePerPole,
    [param: Required, RegularExpression("^[A-Z]{3}$")]
    string Currency,
    int? ExpectedLatestPriceVersion,
    [param: Required, StringLength(100, MinimumLength = 1)]
    string ReasonCode,
    [param: StringLength(500)]
    string? EvidenceReference);

public sealed record PublishSurveyDepositPolicyApiRequest(
    [param: Range(typeof(decimal), "1", "1000000000")]
    decimal Amount,
    [param: Required, RegularExpression("^[A-Z]{3}$")]
    string Currency,
    int? ExpectedLatestVersion,
    [param: Required, StringLength(100, MinimumLength = 1)]
    string ReasonCode,
    [param: StringLength(500)]
    string? EvidenceReference);

public sealed record PublishClaimPolicyApiRequest(
    [param: Range(1, 365)]
    int CalendarWindowDays,
    [param: Required, StringLength(100, MinimumLength = 1)]
    string TimeZoneId,
    bool ExcludeNoticeDate,
    int? ExpectedLatestVersion,
    [param: Required, StringLength(100, MinimumLength = 1)]
    string ReasonCode,
    [param: StringLength(500)]
    string? EvidenceReference);

public sealed record PublishCancellationPolicyApiRequest(
    bool RefundableBeforeCapture,
    bool RetainDepositAfterCapture,
    [param: Range(0, 100)]
    int AllowedFreeNoShowReschedules,
    bool OperationalPostponementRefundable,
    bool ServiceFailureRefundable,
    int? ExpectedLatestVersion,
    [param: Required, StringLength(100, MinimumLength = 1)]
    string ReasonCode,
    [param: StringLength(500)]
    string? EvidenceReference);

public sealed record PublishBusinessPolicyVersionApiRequest(
    string? StreamKey,
    [param: Required]
    IReadOnlyList<Guid> ServiceRevisionIds,
    [param: Required]
    Guid DepositPolicyId,
    [param: Required]
    Guid ClaimPolicyId,
    [param: Required]
    Guid CancellationPolicyId,
    [param: RegularExpression("^[A-Z]{3}$")]
    string? PricingCurrency,
    int? ExpectedLatestVersion,
    [param: Required, StringLength(100, MinimumLength = 1)]
    string ReasonCode,
    [param: StringLength(500)]
    string? EvidenceReference);

public sealed record TransitionBusinessPolicyActivationApiRequest(
    [param: Required]
    Guid TargetPolicyVersionId,
    bool PublicIntakeEnabled,
    bool CaptureEnabled,
    bool ReleaseEnabled,
    Guid? ExpectedLastDecisionId,
    [param: Required, StringLength(100, MinimumLength = 1)]
    string DecisionReasonCode,
    [param: StringLength(500)]
    string? DecisionEvidenceReference);
