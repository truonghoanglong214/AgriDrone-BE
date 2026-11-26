using AgriDrone.Modules.Surveys.Domain;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Policies.ReadCommercialPolicies;

public sealed record GetCommercialServicesQuery(int Offset, int Limit) : IRequest<CommercialServiceItem[]>;

public sealed record CommercialServiceItem(
    Guid Id,
    string Code,
    string Name,
    string Description,
    SurveyServiceStatus Status);

public sealed record GetCommercialRevisionsQuery(Guid? SurveyServiceId, int Offset, int Limit) : IRequest<CommercialRevisionItem[]>;

public sealed record CommercialRevisionItem(
    Guid Id,
    int VersionNumber,
    Guid? PredecessorVersionId,
    Guid SurveyServiceId,
    string Name,
    string Description,
    bool MapsPoles,
    bool ScreensDisease,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string ReasonCode,
    string? EvidenceReference);

public sealed record GetCommercialPricesQuery(Guid? SurveyServiceId, int Offset, int Limit) : IRequest<CommercialPriceItem[]>;

public sealed record CommercialPriceItem(
    Guid Id,
    int VersionNumber,
    Guid? PredecessorVersionId,
    Guid SurveyServiceId,
    decimal PricePerPole,
    string Currency,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? ReasonCode,
    string? EvidenceReference);

public sealed record GetCommercialDepositPoliciesQuery(int Offset, int Limit) : IRequest<CommercialDepositPolicyItem[]>;

public sealed record CommercialDepositPolicyItem(
    Guid Id,
    int VersionNumber,
    Guid? PredecessorVersionId,
    decimal Amount,
    string Currency,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string ReasonCode,
    string? EvidenceReference);

public sealed record GetCommercialClaimPoliciesQuery(int Offset, int Limit) : IRequest<CommercialClaimPolicyItem[]>;

public sealed record CommercialClaimPolicyItem(
    Guid Id,
    int VersionNumber,
    Guid? PredecessorVersionId,
    int CalendarWindowDays,
    string TimeZoneId,
    bool ExcludeNoticeDate,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string ReasonCode,
    string? EvidenceReference);

public sealed record GetCommercialCancellationPoliciesQuery(int Offset, int Limit) : IRequest<CommercialCancellationPolicyItem[]>;

public sealed record CommercialCancellationPolicyItem(
    Guid Id,
    int VersionNumber,
    Guid? PredecessorVersionId,
    bool RefundableBeforeCapture,
    bool RetainDepositAfterCapture,
    int AllowedFreeNoShowReschedules,
    bool OperationalPostponementRefundable,
    bool ServiceFailureRefundable,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string ReasonCode,
    string? EvidenceReference);

public sealed record GetCommercialBundleVersionsQuery(string? StreamKey, int Offset, int Limit) : IRequest<CommercialBundleVersionItem[]>;

public sealed record CommercialBundleVersionItem(
    Guid Id,
    int VersionNumber,
    Guid? PredecessorVersionId,
    string StreamKey,
    Guid SurveyDepositPolicyId,
    Guid ClaimPolicyId,
    Guid CancellationPolicyId,
    Guid[] ServiceRevisionIds,
    string PricingCurrency,
    string PolicyFingerprint,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string ReasonCode,
    string? EvidenceReference);

public sealed record GetCommercialActivationQuery : IRequest<CommercialActivationItem?>;

public sealed record CommercialActivationItem(
    Guid ActiveBusinessPolicyVersionId,
    bool PublicIntakeEnabled,
    bool CaptureEnabled,
    bool ReleaseEnabled,
    Guid LastDecisionId,
    DateTimeOffset ActivatedAt,
    string ReasonCode,
    string? EvidenceReference);
