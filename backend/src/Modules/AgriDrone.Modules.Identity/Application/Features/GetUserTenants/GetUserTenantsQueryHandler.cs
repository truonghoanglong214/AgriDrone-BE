using AgriDrone.SharedKernel.Application;
using AgriDrone.Modules.Identity.Application.Abstractions.Queries;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.GetUserTenants;

internal sealed class GetUserTenantsQueryHandler(
    ITenantMembershipQueries tenantMembershipQueries)
    : IRequestHandler<
        GetUserTenantsQuery,
        Result<PagedResult<UserTenantListItemResponse>>>
{
    public async Task<Result<PagedResult<UserTenantListItemResponse>>> Handle(
        GetUserTenantsQuery request,
        CancellationToken cancellationToken)
    {
        var pageRequest = new PagedRequest(
            request.PageNumber,
            request.PageSize);

        var tenants = await tenantMembershipQueries.GetUserTenantsAsync(
            request.UserId,
            pageRequest,
            cancellationToken);

        return Result.Success(tenants);
    }
}
