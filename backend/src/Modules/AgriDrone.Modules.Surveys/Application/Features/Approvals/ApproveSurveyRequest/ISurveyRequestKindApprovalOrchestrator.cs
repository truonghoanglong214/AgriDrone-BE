using AgriDrone.Modules.Surveys.Application.Features.Approvals.Common;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Features.Approvals.ApproveSurveyRequest;

internal interface ISurveyRequestKindApprovalOrchestrator
{
    SurveyRequestKind RequestKind { get; }

    Task<Result<SurveyApprovalOrchestrationResult>> OrchestrateAsync(
        SurveyRequest request,
        SurveyApprovalPrerequisites prerequisites,
        SurveyApprovalExecutionContext context,
        CancellationToken cancellationToken);
}

internal sealed record SurveyApprovalExecutionContext(
    Guid ActorId,
    DateTimeOffset ApprovedAt);

internal sealed record SurveyApprovalOrchestrationResult(
    SurveyOrder Order,
    Guid TenantId,
    Guid FarmId,
    Guid SystemManagerProfileId,
    Guid? PrimaryManagerAssignmentId,
    Guid? OwnerInvitationId);
