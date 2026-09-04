using AgriDrone.SharedKernel.Application;
using AgriDrone.Modules.Identity.Application.Abstractions.Queries;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;
using AgriDrone.Modules.Identity.Application.Errors;
using AgriDrone.SharedKernel.Application.Abstractions;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Identity.Application.Features.GetTenantUsers
{
    internal sealed class GetTenantUsersQueryHandler(
        ITenantMembershipQueries tenantMembershipQueries,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        IEffectiveAccessService effectiveAccessService) : IRequestHandler<GetTenantUsersQuery, Result<PagedResult<TenantUsersListItemResponse>>>
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

            if (currentUser.UserId is not Guid userId)
            {
                return Result.Failure<PagedResult<TenantUsersListItemResponse>>(
                    AuthenticationError.CurrentUserRequired());
            }

            var access = await effectiveAccessService.CheckTenantAsync(
                userId,
                tenantId,
                TenantAccessLevel.Admin,
                cancellationToken);

            if (!access.IsAllowed)
            {
                return Result.Failure<PagedResult<TenantUsersListItemResponse>>(
                    TenantError.AccessDenied());
            }

            var users = await tenantMembershipQueries.GetUsersPageAsync(
                tenantId,
                pageRequest,
                cancellationToken);

            return Result.Success(users);
        }
    }
}
