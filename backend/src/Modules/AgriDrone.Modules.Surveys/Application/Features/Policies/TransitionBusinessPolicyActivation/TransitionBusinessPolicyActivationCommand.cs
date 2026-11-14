using AgriDrone.SharedKernel.Application;
using MediatR;

namespace AgriDrone.Modules.Surveys.Application.Features.Policies.TransitionBusinessPolicyActivation;

public sealed record TransitionBusinessPolicyActivationCommand(
    Guid BusinessPolicyVersionId,
    bool PublicIntakeEnabled,
    bool CaptureEnabled,
    bool ReleaseEnabled,
    Guid? ExpectedLastDecisionId,
    string ReasonCode,
    string? EvidenceReference = null)
    : IRequest<Result<TransitionBusinessPolicyActivationResponse>>;

public sealed record TransitionBusinessPolicyActivationResponse(
    Guid ActivationId,
    Guid ActiveBusinessPolicyVersionId,
    bool PublicIntakeEnabled,
    bool CaptureEnabled,
    bool ReleaseEnabled,
    Guid LastDecisionId,
    DateTimeOffset ActivatedAt,
    string ReasonCode,
    string? EvidenceReference);
