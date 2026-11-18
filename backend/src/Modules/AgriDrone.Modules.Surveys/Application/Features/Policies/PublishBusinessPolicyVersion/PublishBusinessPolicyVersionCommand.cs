using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Policies.PublishBusinessPolicyVersion;

public sealed record PublishBusinessPolicyVersionCommand(
    string StreamKey,
    Guid SurveyDepositPolicyId,
    Guid ClaimPolicyId,
    Guid CancellationPolicyId,
    IReadOnlyCollection<Guid> ServiceRevisionIds,
    string PricingCurrency,
    int? ExpectedLatestVersion,
    string ReasonCode,
    string? EvidenceReference = null)
    : IRequest<Result<PublishBusinessPolicyVersionResponse>>;

public sealed record PublishBusinessPolicyVersionResponse(
    Guid PolicyVersionId,
    string StreamKey,
    int VersionNumber,
    Guid? PredecessorVersionId,
    Guid SurveyDepositPolicyId,
    Guid ClaimPolicyId,
    Guid CancellationPolicyId,
    IReadOnlyCollection<Guid> ServiceRevisionIds,
    string PricingCurrency,
    string PolicyFingerprint,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string ReasonCode,
    string? EvidenceReference);
