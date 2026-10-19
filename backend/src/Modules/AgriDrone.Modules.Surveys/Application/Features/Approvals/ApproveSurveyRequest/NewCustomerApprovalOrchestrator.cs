using AgriDrone.Modules.Surveys.Application.Abstractions.Approvals;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Application.Features.Approvals.Common;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Features.Approvals.ApproveSurveyRequest;

internal sealed class NewCustomerApprovalOrchestrator(
    ISurveyApprovalProvisioningPort provisioningPort,
    ISurveyApprovalNumberGenerator numberGenerator,
    ISurveyOrderRepository orderRepository)
    : ISurveyRequestKindApprovalOrchestrator
{
    public SurveyRequestKind RequestKind => SurveyRequestKind.NewCustomer;

    public async Task<Result<SurveyApprovalOrchestrationResult>>
        OrchestrateAsync(
            SurveyRequest request,
            SurveyApprovalPrerequisites prerequisites,
            SurveyApprovalExecutionContext context,
            CancellationToken cancellationToken)
    {
        if (request.Kind != RequestKind ||
            request.TenantId.HasValue ||
            request.FarmId.HasValue ||
            request.RequestedByUserId.HasValue ||
            string.IsNullOrWhiteSpace(request.TenantCode) ||
            prerequisites.TenantOwner is not null ||
            prerequisites.Farm is not null ||
            prerequisites.CurrentPrimaryAssignment is not null)
        {
            return Result.Failure<SurveyApprovalOrchestrationResult>(
                SurveyApprovalError.RequestContextInvalid());
        }

        var tenantResult = await provisioningPort.StageTenantAsync(
            new StageSurveyApprovalTenant(
                request.TenantCode,
                request.TenantName ?? request.ApplicantName,
                context.ApprovedAt),
            cancellationToken);
        if (tenantResult.IsFailure)
        {
            return Result.Failure<SurveyApprovalOrchestrationResult>(
                tenantResult.Error);
        }

        var farmResult = await provisioningPort.StageFarmAsync(
            new StageSurveyApprovalFarm(
                tenantResult.Value.TenantId,
                context.ActorId,
                numberGenerator.CreateFarmCode(),
                request.FarmName,
                request.FarmAddress,
                request.ApproximateAreaHa,
                request.MapLocation.X,
                request.MapLocation.Y,
                request.MapLocation.SRID,
                context.ApprovedAt),
            cancellationToken);
        if (farmResult.IsFailure)
        {
            return Result.Failure<SurveyApprovalOrchestrationResult>(
                farmResult.Error);
        }

        var order = SurveyOrder.Create(
            numberGenerator.CreateOrderNumber(),
            tenantResult.Value.TenantId,
            farmResult.Value.FarmId,
            request.Id,
            request.SurveyServiceId,
            requiresBaselineMapping: true,
            prerequisites.OrderCompatibility.PreviousCompatibleOrderId,
            prerequisites.OrderCompatibility.PolicyVersion,
            prerequisites.OrderCompatibility.Reason,
            context.ApprovedAt);
        orderRepository.Add(order);

        var assignmentResult = await provisioningPort
            .StagePrimaryManagerAssignmentAsync(
                new StageSurveyApprovalPrimaryAssignment(
                    tenantResult.Value.TenantId,
                    farmResult.Value.FarmId,
                    prerequisites.Manager.SystemManagerProfileId,
                    context.ActorId,
                    CreateAssignmentReason(request.RequestNumber),
                    context.ApprovedAt),
                cancellationToken);
        if (assignmentResult.IsFailure)
        {
            return Result.Failure<SurveyApprovalOrchestrationResult>(
                assignmentResult.Error);
        }

        var invitationResult = await provisioningPort.StageOwnerInvitationAsync(
            new StageSurveyApprovalOwnerInvitation(
                tenantResult.Value.TenantId,
                context.ActorId,
                request.ApplicantEmail,
                context.ApprovedAt),
            cancellationToken);
        if (invitationResult.IsFailure)
        {
            return Result.Failure<SurveyApprovalOrchestrationResult>(
                invitationResult.Error);
        }

        return Result.Success(new SurveyApprovalOrchestrationResult(
            order,
            tenantResult.Value.TenantId,
            farmResult.Value.FarmId,
            prerequisites.Manager.SystemManagerProfileId,
            assignmentResult.Value.AssignmentId,
            invitationResult.Value.InvitationId));
    }

    private static string CreateAssignmentReason(string requestNumber) =>
        $"Primary assignment created by approval of survey request {requestNumber}.";
}
