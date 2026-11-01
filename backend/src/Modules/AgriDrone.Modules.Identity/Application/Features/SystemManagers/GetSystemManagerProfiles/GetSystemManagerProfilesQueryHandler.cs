using AgriDrone.Modules.Identity.Application.Abstractions.Queries;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;

namespace AgriDrone.Modules.Identity.Application.Features.SystemManagers.GetSystemManagerProfiles;

internal sealed class GetSystemManagerProfilesQueryHandler(
    ISystemManagerProfileQueries queries)
    : IRequestHandler<
        GetSystemManagerProfilesQuery,
        Result<PagedResult<SystemManagerProfileResponse>>>
{
    public async Task<Result<PagedResult<SystemManagerProfileResponse>>> Handle(
        GetSystemManagerProfilesQuery request,
        CancellationToken cancellationToken)
    {
        var profiles = await queries.GetPageAsync(
            request.Status,
            request.Availability,
            new PagedRequest(request.PageNumber, request.PageSize),
            cancellationToken);

        return Result.Success(profiles);
    }
}
