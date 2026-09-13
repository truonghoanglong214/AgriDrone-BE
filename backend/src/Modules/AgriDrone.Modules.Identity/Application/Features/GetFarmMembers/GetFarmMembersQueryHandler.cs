using AgriDrone.IntegrationContracts.Farms;
using AgriDrone.Modules.Identity.Application.Abstractions.Queries;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.GetFarmMembers;

internal sealed class GetFarmMembersQueryHandler(
    IFarmMembershipQueries farmMembershipQueries,
    IFarmAssignmentReferenceQuery farmReferenceQuery,
    IExecutionContext executionContext,
    IEffectiveAccessService effectiveAccessService)
    : IRequestHandler<
        GetFarmMembersQuery,
        Result<PagedResult<FarmMemberListItemResponse>>>
{
    public async Task<Result<PagedResult<FarmMemberListItemResponse>>> Handle(
        GetFarmMembersQuery request,
        CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is not Guid tenantId)
        {
            return Result.Failure<PagedResult<FarmMemberListItemResponse>>(
                AuthenticationError.CurrentTenantRequired());
        }

        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<PagedResult<FarmMemberListItemResponse>>(
                AuthenticationError.CurrentUserRequired());
        }

        var hasAccess = await effectiveAccessService.CheckTenantAsync(
            actorId, 
            tenantId, 
            TenantAccessLevel.Admin,
            cancellationToken);

        if (!hasAccess.IsAllowed)
        {
            return Result.Failure<PagedResult<FarmMemberListItemResponse>>(
                TenantError.AccessDenied());
        }

        var isActiveFarm = await farmReferenceQuery.IsActiveFarmAsync(
            tenantId,
            request.FarmId,
            cancellationToken);

        if (!isActiveFarm)
        {
            return Result.Failure<PagedResult<FarmMemberListItemResponse>>(
                FarmMembershipError.FarmNotFound());
        }

        var pageRequest = new PagedRequest(
            request.PageNumber,
            request.PageSize);
        var farmMembers = await farmMembershipQueries.GetMembersPageAsync(
            tenantId,
            request.FarmId,
            request.Role,
            request.Status,
            pageRequest,
            cancellationToken);

        return Result.Success(farmMembers);
    }
}
