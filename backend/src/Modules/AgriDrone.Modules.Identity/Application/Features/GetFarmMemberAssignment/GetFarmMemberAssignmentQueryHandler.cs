using AgriDrone.IntegrationContracts.Farms;
using AgriDrone.Modules.Identity.Application.Abstractions.Queries;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.GetFarmMemberAssignment;

internal sealed class GetFarmMemberAssignmentQueryHandler(
    IFarmMembershipQueries farmMembershipQueries,
    IFarmAssignmentReferenceQuery farmReferenceQuery,
    IExecutionContext executionContext,
    IEffectiveAccessService effectiveAccessService)
    : IRequestHandler<
        GetFarmMemberAssignmentQuery,
        Result<GetFarmMemberAssignmentResponse>>
{
    public async Task<Result<GetFarmMemberAssignmentResponse>> Handle(
        GetFarmMemberAssignmentQuery request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<GetFarmMemberAssignmentResponse>(
                AuthenticationError.CurrentUserRequired());
        }

        if (executionContext.TenantId is not Guid tenantId)
        {
            return Result.Failure<GetFarmMemberAssignmentResponse>(
                TenantError.ContextRequired());
        }

        var accessDecision = await effectiveAccessService.CheckTenantAsync(
            actorId,
            tenantId,
            TenantAccessLevel.Admin,
            cancellationToken);

        if (!accessDecision.IsAllowed)
        {
            return Result.Failure<GetFarmMemberAssignmentResponse>(
                TenantError.AccessDenied());
        }

        if (!await farmReferenceQuery.IsActiveFarmAsync(
                tenantId,
                request.FarmId,
                cancellationToken))
        {
            return Result.Failure<GetFarmMemberAssignmentResponse>(
                FarmMembershipError.FarmNotFound());
        }

        var assignment = await farmMembershipQueries.GetAssignmentAsync(
            tenantId,
            request.FarmId,
            request.UserId,
            cancellationToken);

        return assignment is null
            ? Result.Failure<GetFarmMemberAssignmentResponse>(
                FarmMembershipError.NotFound())
            : Result.Success(assignment);
    }
}
