using AgriDrone.Modules.Surveys.Application.Abstractions.Approvals;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Application.Features.Approvals.Common;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Features.Approvals.ApproveSurveyRequest;

internal sealed class ExistingFarmSurveyApprovalOrchestrator(
    ISurveyApprovalNumberGenerator numberGenerator,
    ISurveyOrderRepository orderRepository)
    : ISurveyRequestKindApprovalOrchestrator
{
    public SurveyRequestKind RequestKind => SurveyRequestKind.ExistingFarmSurvey;

    public Task<Result<SurveyApprovalOrchestrationResult>> OrchestrateAsync(
        SurveyRequest request,
        SurveyApprovalPrerequisites prerequisites,
        SurveyApprovalExecutionContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (request.Kind != RequestKind ||
            request.TenantId is not Guid tenantId ||
            request.FarmId is not Guid farmId ||
            request.RequestedByUserId is not Guid requestedByUserId ||
            prerequisites.TenantOwner is not { } owner ||
            owner.TenantId != tenantId ||
            owner.UserId != requestedByUserId ||
            prerequisites.Farm is not { } farm ||
            farm.TenantId != tenantId ||
            farm.FarmId != farmId ||
            prerequisites.CurrentPrimaryAssignment is not { } assignment ||
            assignment.TenantId != tenantId ||
            assignment.FarmId != farmId ||
            assignment.SystemManagerProfileId !=
                prerequisites.Manager.SystemManagerProfileId ||
            prerequisites.FarmBaseline is null)
        {
            return Task.FromResult(
                Result.Failure<SurveyApprovalOrchestrationResult>(
                    SurveyApprovalError.RequestContextInvalid()));
        }

        var order = SurveyOrder.Create(
            numberGenerator.CreateOrderNumber(),
            tenantId,
            farmId,
            request.Id,
            request.SurveyServiceId,
            prerequisites.RequiresBaselineMapping,
            prerequisites.OrderCompatibility.PreviousCompatibleOrderId,
            prerequisites.OrderCompatibility.PolicyVersion,
            prerequisites.OrderCompatibility.Reason,
            context.ApprovedAt);
        orderRepository.Add(order);

        return Task.FromResult(Result.Success(
            new SurveyApprovalOrchestrationResult(
                order,
                tenantId,
                farmId,
                prerequisites.Manager.SystemManagerProfileId,
                assignment.AssignmentId,
                OwnerInvitationId: null)));
    }
}
