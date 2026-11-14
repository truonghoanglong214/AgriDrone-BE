using AgriDrone.Modules.Surveys.Application.Abstractions.Approvals;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Application.Features.Approvals.Common;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Features.Approvals.ApproveSurveyRequest;

internal sealed class ExistingTenantNewFarmApprovalOrchestrator(
    ISurveyApprovalProvisioningPort provisioningPort,
    ISurveyApprovalNumberGenerator numberGenerator,
    ISurveyOrderRepository orderRepository)
    : ISurveyRequestKindApprovalOrchestrator
{
    public SurveyRequestKind RequestKind =>
        SurveyRequestKind.ExistingTenantNewFarm;

    public async Task<Result<SurveyApprovalOrchestrationResult>>
        OrchestrateAsync(
            SurveyRequest request,
            SurveyApprovalPrerequisites prerequisites,
            SurveyApprovalExecutionContext context,
            CancellationToken cancellationToken)
    {
        if (request.Kind != RequestKind ||
            request.TenantId is not Guid tenantId ||
            request.FarmId.HasValue ||
            request.RequestedByUserId is not Guid requestedByUserId ||
            prerequisites.TenantOwner is not { } owner ||
            owner.TenantId != tenantId ||
            owner.UserId != requestedByUserId ||
            prerequisites.Farm is not null ||
            prerequisites.CurrentPrimaryAssignment is not null)
        {
            return Result.Failure<SurveyApprovalOrchestrationResult>(
                SurveyApprovalError.RequestContextInvalid());
        }

        var farmResult = await provisioningPort.StageFarmAsync(
            new StageSurveyApprovalFarm(
                tenantId,
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

        SurveyOrder order;
        if (request.SurveyService.Scope == ContractScope.V3)
        {
            if (context.CommercialSnapshot is null)
            {
                return Result.Failure<SurveyApprovalOrchestrationResult>(
                    SurveyApprovalError.CommercialPolicyMismatch("Commercial snapshot is required for V3 survey order."));
            }

            order = SurveyOrder.CreateV3(
                numberGenerator.CreateOrderNumber(),
                tenantId,
                farmResult.Value.FarmId,
                request.Id,
                request.SurveyServiceId,
                context.CommercialSnapshot,
                requiresBaselineMapping: true,
                previousCompatibleOrderId: prerequisites.OrderCompatibility.PreviousCompatibleOrderId,
                previousOrderCompatibilityPolicyVersion: prerequisites.OrderCompatibility.PolicyVersion,
                previousOrderCompatibilityReason: prerequisites.OrderCompatibility.Reason,
                createdAt: context.ApprovedAt);
            orderRepository.Add(order);

            var commercialSnapshot = SurveyOrderCommercialSnapshot.Create(
                order.Id,
                context.CommercialSnapshot,
                context.ApprovedAt);
            orderRepository.AddCommercialSnapshot(commercialSnapshot);

            var initialHistory = OrderStateHistory.CreateInitial(
                Guid.NewGuid(),
                order.Id,
                order.Version,
                order.Status,
                context.ActorId,
                "APPROVAL",
                $"Survey order created by approval of survey request {request.RequestNumber}.",
                context.CorrelationId,
                context.ApprovedAt);
            orderRepository.AddStateHistory(initialHistory);
        }
        else
        {
            order = SurveyOrder.Create(
                numberGenerator.CreateOrderNumber(),
                tenantId,
                farmResult.Value.FarmId,
                request.Id,
                request.SurveyServiceId,
                requiresBaselineMapping: true,
                prerequisites.OrderCompatibility.PreviousCompatibleOrderId,
                prerequisites.OrderCompatibility.PolicyVersion,
                prerequisites.OrderCompatibility.Reason,
                context.ApprovedAt,
                prerequisites.ApplicableDepositAmount,
                prerequisites.ApplicableComboDiscountPercentage);
            order.EnablePostFlightSettlement(prerequisites.Service.ServiceType!.Value,
                prerequisites.Service.PriceId!.Value, prerequisites.Service.PricePerPole!.Value);
            orderRepository.Add(order);
        }

        var assignmentResult = await provisioningPort
            .StagePrimaryManagerAssignmentAsync(
                new StageSurveyApprovalPrimaryAssignment(
                    tenantId,
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

        return Result.Success(new SurveyApprovalOrchestrationResult(
            order,
            tenantId,
            farmResult.Value.FarmId,
            prerequisites.Manager.SystemManagerProfileId,
            assignmentResult.Value.AssignmentId,
            OwnerInvitationId: null));
    }

    private static string CreateAssignmentReason(string requestNumber) =>
        $"Primary assignment created by approval of survey request {requestNumber}.";
}
