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
                prerequisites.Manager.SystemManagerProfileId)
        {
            return Task.FromResult(
                Result.Failure<SurveyApprovalOrchestrationResult>(
                    SurveyApprovalError.RequestContextInvalid()));
        }

        SurveyOrder order;
        if (request.SurveyService.Scope == ContractScope.V3)
        {
            if (prerequisites.FarmBaseline is null ||
                !prerequisites.FarmBaseline.CurrentPublishedBaseMapVersionId.HasValue ||
                prerequisites.RequiresBaselineMapping)
            {
                return Task.FromResult(
                    Result.Failure<SurveyApprovalOrchestrationResult>(
                        SurveyApprovalError.RequestContextInvalid()));
            }
            if (context.CommercialSnapshot is null)
            {
                return Task.FromResult(Result.Failure<SurveyApprovalOrchestrationResult>(
                    SurveyApprovalError.CommercialPolicyMismatch("Commercial snapshot is required for V3 survey order.")));
            }

            order = SurveyOrder.CreateV3(
                numberGenerator.CreateOrderNumber(),
                tenantId,
                farmId,
                request.Id,
                request.SurveyServiceId,
                context.CommercialSnapshot,
                requiresBaselineMapping: false,
                farmBaseMapVersionId: prerequisites.FarmBaseline.CurrentPublishedBaseMapVersionId.Value,
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
                farmId,
                request.Id,
                request.SurveyServiceId,
                prerequisites.RequiresBaselineMapping,
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
