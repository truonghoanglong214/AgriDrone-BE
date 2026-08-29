using AgriDrone.Modules.Identity.Application.Features.GetUsers;
using AgriDrone.SharedKernel.Application.Pagination;
namespace AgriDrone.Modules.Identity.Application.Abstractions.Queries
{
    internal interface IUserQueries
    {
        Task<PagedResult<UserListItemResponse>> GetPageAsync(PagedRequest pagedRequest, CancellationToken cancellationToken);

    }
}
