using AgriDrone.Modules.Surveys.Application.Abstractions.Queries;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Application.Features.Approvals.Common;

internal interface ISurveyApprovalPrerequisiteResolver
{
    Task<Result<SurveyApprovalPrerequisites>> ResolveAsync(
        SurveyRequest request,
        Guid? selectedSystemManagerProfileId,
        DateTimeOffset approvalTime,
        CancellationToken cancellationToken = default);
}

internal sealed class SurveyApprovalPrerequisiteResolver(
    ISurveyApprovalReferenceQueries referenceQueries,
    ISurveyDepositPolicyRepository depositPolicyRepository,
    ISurveyComboDiscountPolicyRepository comboDiscountPolicyRepository)
    : ISurveyApprovalPrerequisiteResolver
{
    public async Task<Result<SurveyApprovalPrerequisites>> ResolveAsync(
        SurveyRequest request,
        Guid? selectedSystemManagerProfileId,
        DateTimeOffset approvalTime,
        CancellationToken cancellationToken = default)
    {
        _ = comboDiscountPolicyRepository; // Legacy policy endpoints remain for legacy orders.
        ArgumentNullException.ThrowIfNull(request);
        DomainGuard.Utc(approvalTime);

        if (request.Status != SurveyRequestStatus.UnderReview)
        {
            return Result.Failure<SurveyApprovalPrerequisites>(
                SurveyApprovalError.RequestNotUnderReview(request.Id));
        }

        var service = await referenceQueries.GetServiceForApprovalAsync(
            request.SurveyServiceId,
            cancellationToken);
        if (service is null ||
            service.Status != SurveyServiceStatus.Active ||
            service.ServiceType == SurveyServiceType.HarvestReadiness ||
            service.PriceId is null || service.PricePerPole is null)
        {
            return Result.Failure<SurveyApprovalPrerequisites>(
                SurveyApprovalError.ServiceUnavailable());
        }

        var activeDepositPolicy = await depositPolicyRepository.GetCurrentAsync(cancellationToken);
        var applicableDeposit = activeDepositPolicy?.AmountVnd ?? SurveyOrder.DefaultBaselineDepositAmountVnd;
        if (applicableDeposit <= 0m)
            return Result.Failure<SurveyApprovalPrerequisites>(AppError.Conflict(
                "SurveyDepositPolicy.PositiveDepositRequired",
                "Registration v2.6 requires a positive shared deposit before approving a new order."));

        // Registration v2.6 has no Combo discount or Follow-up price tier.
        const decimal applicableDiscount = 0m;

        return request.Kind switch
        {
            SurveyRequestKind.NewCustomer =>
                await ResolveNewCustomerAsync(
                    request,
                    service,
                    selectedSystemManagerProfileId,
                    approvalTime,
                    applicableDeposit,
                    applicableDiscount,
                    cancellationToken),
            SurveyRequestKind.ExistingTenantNewFarm =>
                await ResolveExistingTenantNewFarmAsync(
                    request,
                    service,
                    selectedSystemManagerProfileId,
                    approvalTime,
                    applicableDeposit,
                    applicableDiscount,
                    cancellationToken),
            SurveyRequestKind.ExistingFarmSurvey =>
                await ResolveExistingFarmAsync(
                    request,
                    service,
                    selectedSystemManagerProfileId,
                    approvalTime,
                    applicableDeposit,
                    applicableDiscount,
                    cancellationToken),
            _ => Result.Failure<SurveyApprovalPrerequisites>(
                SurveyApprovalError.RequestContextInvalid())
        };
    }

    private async Task<Result<SurveyApprovalPrerequisites>>
        ResolveNewCustomerAsync(
            SurveyRequest request,
            SurveyApprovalServiceReference service,
            Guid? selectedManagerId,
            DateTimeOffset approvalTime,
            decimal applicableDeposit,
            decimal applicableDiscount,
            CancellationToken cancellationToken)
    {
        if (request.TenantId.HasValue ||
            request.FarmId.HasValue ||
            request.RequestedByUserId.HasValue)
        {
            return Result.Failure<SurveyApprovalPrerequisites>(
                SurveyApprovalError.RequestContextInvalid());
        }

        var managerResult = await ResolveSelectedManagerAsync(
            selectedManagerId,
            approvalTime,
            cancellationToken);
        if (managerResult.IsFailure)
        {
            return Result.Failure<SurveyApprovalPrerequisites>(
                managerResult.Error);
        }

        return Result.Success(new SurveyApprovalPrerequisites(
            service,
            managerResult.Value,
            TenantOwner: null,
            Farm: null,
            CurrentPrimaryAssignment: null,
            FarmBaseline: null,
            RequiresBaselineMapping: true,
            OrderCompatibility:
                SurveyApprovalOrderCompatibilityPolicy.ForNewFarm(),
            ApplicableDepositAmount: applicableDeposit,
            ApplicableComboDiscountPercentage: applicableDiscount));
    }

    private async Task<Result<SurveyApprovalPrerequisites>>
        ResolveExistingTenantNewFarmAsync(
            SurveyRequest request,
            SurveyApprovalServiceReference service,
            Guid? selectedManagerId,
            DateTimeOffset approvalTime,
            decimal applicableDeposit,
            decimal applicableDiscount,
            CancellationToken cancellationToken)
    {
        if (request.TenantId is not Guid tenantId ||
            request.FarmId.HasValue ||
            request.RequestedByUserId is not Guid requestedByUserId)
        {
            return Result.Failure<SurveyApprovalPrerequisites>(
                SurveyApprovalError.RequestContextInvalid());
        }

        var ownerResult = await ResolveOwnerAsync(
            tenantId,
            requestedByUserId,
            cancellationToken);
        if (ownerResult.IsFailure)
        {
            return Result.Failure<SurveyApprovalPrerequisites>(
                ownerResult.Error);
        }

        var managerResult = await ResolveSelectedManagerAsync(
            selectedManagerId,
            approvalTime,
            cancellationToken);
        if (managerResult.IsFailure)
        {
            return Result.Failure<SurveyApprovalPrerequisites>(
                managerResult.Error);
        }

        return Result.Success(new SurveyApprovalPrerequisites(
            service,
            managerResult.Value,
            ownerResult.Value,
            Farm: null,
            CurrentPrimaryAssignment: null,
            FarmBaseline: null,
            RequiresBaselineMapping: true,
            OrderCompatibility:
                SurveyApprovalOrderCompatibilityPolicy.ForNewFarm(),
            ApplicableDepositAmount: applicableDeposit,
            ApplicableComboDiscountPercentage: applicableDiscount));
    }

    private async Task<Result<SurveyApprovalPrerequisites>>
        ResolveExistingFarmAsync(
            SurveyRequest request,
            SurveyApprovalServiceReference service,
            Guid? selectedManagerId,
            DateTimeOffset approvalTime,
            decimal applicableDeposit,
            decimal applicableDiscount,
            CancellationToken cancellationToken)
    {
        if (request.TenantId is not Guid tenantId ||
            request.FarmId is not Guid farmId ||
            request.RequestedByUserId is not Guid requestedByUserId)
        {
            return Result.Failure<SurveyApprovalPrerequisites>(
                SurveyApprovalError.RequestContextInvalid());
        }

        var ownerResult = await ResolveOwnerAsync(
            tenantId,
            requestedByUserId,
            cancellationToken);
        if (ownerResult.IsFailure)
        {
            return Result.Failure<SurveyApprovalPrerequisites>(
                ownerResult.Error);
        }

        var farm = await referenceQueries.GetFarmForApprovalAsync(
            tenantId,
            farmId,
            cancellationToken);
        if (farm is null || !farm.IsActive)
        {
            return Result.Failure<SurveyApprovalPrerequisites>(
                SurveyApprovalError.FarmUnavailable());
        }

        var assignment = await referenceQueries
            .GetActivePrimaryAssignmentForApprovalAsync(
                tenantId,
                farmId,
                cancellationToken);
        if (assignment is null)
        {
            return Result.Failure<SurveyApprovalPrerequisites>(
                SurveyApprovalError.PrimaryManagerRequired());
        }

        if (selectedManagerId.HasValue &&
            selectedManagerId.Value != assignment.SystemManagerProfileId)
        {
            return Result.Failure<SurveyApprovalPrerequisites>(
                SurveyApprovalError.ExistingFarmManagerMismatch());
        }

        var manager = await referenceQueries.GetManagerForApprovalAsync(
            assignment.SystemManagerProfileId,
            cancellationToken);
        if (manager is null ||
            !SurveyApprovalManagerEligibilityPolicy.IsAssignable(
                manager,
                approvalTime))
        {
            return Result.Failure<SurveyApprovalPrerequisites>(
                SurveyApprovalError.ManagerNotAssignable());
        }

        var baseline = await referenceQueries.GetFarmBaselineForApprovalAsync(
            tenantId,
            farmId,
            cancellationToken);
        var previousOrderCandidate = await referenceQueries
            .GetPreviousOrderCandidateForApprovalAsync(
                tenantId,
                farmId,
                request.SurveyServiceId,
                cancellationToken);
        var compatibility = SurveyApprovalOrderCompatibilityPolicy.Evaluate(
            baseline,
            previousOrderCandidate);

        return Result.Success(new SurveyApprovalPrerequisites(
            service,
            manager,
            ownerResult.Value,
            farm,
            assignment,
            baseline,
            SurveyApprovalBaselineRequirementResolver
                .RequiresBaselineMapping(baseline),
            compatibility,
            applicableDeposit,
            applicableDiscount));
    }

    private async Task<Result<SurveyApprovalManagerReference>>
        ResolveSelectedManagerAsync(
            Guid? selectedManagerId,
            DateTimeOffset approvalTime,
            CancellationToken cancellationToken)
    {
        if (!selectedManagerId.HasValue ||
            selectedManagerId.Value == Guid.Empty)
        {
            return Result.Failure<SurveyApprovalManagerReference>(
                SurveyApprovalError.SelectedManagerRequired());
        }

        var manager = await referenceQueries.GetManagerForApprovalAsync(
            selectedManagerId.Value,
            cancellationToken);
        return manager is not null &&
               SurveyApprovalManagerEligibilityPolicy.IsAssignable(
                   manager,
                   approvalTime)
            ? Result.Success(manager)
            : Result.Failure<SurveyApprovalManagerReference>(
                SurveyApprovalError.ManagerNotAssignable());
    }

    private async Task<Result<SurveyApprovalTenantOwnerReference>>
        ResolveOwnerAsync(
            Guid tenantId,
            Guid requestedByUserId,
            CancellationToken cancellationToken)
    {
        var owner = await referenceQueries.GetTenantOwnerForApprovalAsync(
            tenantId,
            requestedByUserId,
            cancellationToken);
        return owner is not null &&
               SurveyApprovalTenantOwnerEligibilityPolicy.IsEligible(owner)
            ? Result.Success(owner)
            : Result.Failure<SurveyApprovalTenantOwnerReference>(
                SurveyApprovalError.TenantOwnerNotEligible());
    }
}
