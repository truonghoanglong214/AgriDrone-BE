using AgriDrone.SharedKernel.Application;
using AgriDrone.Modules.Identity.Application.Abstractions.Queries;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.SharedKernel.Application.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.GetTenantUsers
{
    internal sealed class GetTenantUsersQueryHandler(
        ITenantMembershipQueries tenantMembershipQueries,
        ICurrentTenant currentTenant) : IRequestHandler<GetTenantUsersQuery, Result<PagedResult<TenantUsersListItemResponse>>>
    {
        public async Task<Result<PagedResult<TenantUsersListItemResponse>>> Handle(
            GetTenantUsersQuery request,
            CancellationToken cancellationToken)
        {
            var pageRequest = new PagedRequest(request.PageNumber, request.PageSize);

            if (currentTenant.TenantId is not Guid tenantId)
            {
                return Result.Failure<PagedResult<TenantUsersListItemResponse>>(
                    TenantError.ContextRequired());
            }

            var users = await tenantMembershipQueries.GetUsersPageAsync(
                tenantId,
                pageRequest,
                cancellationToken);

            return Result.Success(users);
        }
    }
}
