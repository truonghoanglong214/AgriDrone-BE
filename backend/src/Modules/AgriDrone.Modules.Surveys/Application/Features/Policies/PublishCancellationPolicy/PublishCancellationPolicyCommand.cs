using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Policies.PublishCancellationPolicy;

public sealed record PublishCancellationPolicyCommand(
    bool RefundableBeforeCapture,
    bool RetainDepositAfterCapture,
    int AllowedFreeNoShowReschedules,
    bool OperationalPostponementRefundable,
    bool ServiceFailureRefundable,
    int? ExpectedLatestVersion,
    string ReasonCode,
    string? EvidenceReference = null)
    : IRequest<Result<PublishCancellationPolicyResponse>>;

public sealed record PublishCancellationPolicyResponse(
    Guid PolicyId,
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
