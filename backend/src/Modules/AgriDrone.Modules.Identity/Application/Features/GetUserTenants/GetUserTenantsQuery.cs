using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.GetUserTenants;

public sealed record GetUserTenantsQuery(
    Guid UserId,
    int PageNumber,
    int PageSize)
    : IRequest<Result<PagedResult<UserTenantListItemResponse>>>;
