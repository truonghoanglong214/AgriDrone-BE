using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Policies.PublishSurveyDepositPolicy;

public sealed record PublishSurveyDepositPolicyCommand(
    decimal Amount,
    string Currency,
    int? ExpectedLatestVersion,
    string ReasonCode,
    string? EvidenceReference = null)
    : IRequest<Result<PublishSurveyDepositPolicyResponse>>;

public sealed record PublishSurveyDepositPolicyResponse(
    Guid PolicyId,
    int VersionNumber,
    Guid? PredecessorVersionId,
    decimal Amount,
    string Currency,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string ReasonCode,
    string? EvidenceReference);
