using AgriDrone.Modules.Farms.Application.Abstractions.Queries;
using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Application.Pagination;
using MediatR;

namespace AgriDrone.Modules.Farms.Application.Features.GetArchivedFarms;

internal sealed class GetArchivedFarmsQueryHandler(
    IFarmQueries farmQueries,
    IExecutionContext executionContext,
    IEffectiveAccessService effectiveAccessService)
    : IRequestHandler<
        GetArchivedFarmsQuery,
        Result<PagedResult<ArchivedFarmResponse>>>
{
    public async Task<Result<PagedResult<ArchivedFarmResponse>>> Handle(
        GetArchivedFarmsQuery request,
        CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is not Guid tenantId)
        {
            return Result.Failure<PagedResult<ArchivedFarmResponse>>(
                AuthenticationError.CurrentTenantRequired());
        }

        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<PagedResult<ArchivedFarmResponse>>(
                AuthenticationError.CurrentUserRequired());
        }

        var access = await effectiveAccessService.CheckTenantAsync(
            actorId,
            tenantId,
            TenantAccessLevel.Owner,
            cancellationToken);

        if (!access.IsAllowed)
        {
            return Result.Failure<PagedResult<ArchivedFarmResponse>>(
                FarmError.AccessDenied());
        }

        var farms = await farmQueries.GetArchivedFarmsPageAsync(
            tenantId,
            new PagedRequest(request.PageNumber, request.PageSize),
            cancellationToken);

        return Result.Success(farms);
    }
}
