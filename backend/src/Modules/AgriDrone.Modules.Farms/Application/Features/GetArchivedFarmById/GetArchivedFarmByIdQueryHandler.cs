using AgriDrone.Modules.Farms.Application.Abstractions.Queries;
using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.Modules.Farms.Application.Features.GetArchivedFarms;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Farms.Application.Features.GetArchivedFarmById;

internal sealed class GetArchivedFarmByIdQueryHandler(
    IFarmQueries farmQueries,
    IExecutionContext executionContext,
    IEffectiveAccessService effectiveAccessService)
    : IRequestHandler<GetArchivedFarmByIdQuery, Result<ArchivedFarmResponse>>
{
    public async Task<Result<ArchivedFarmResponse>> Handle(
        GetArchivedFarmByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is not Guid tenantId)
        {
            return Result.Failure<ArchivedFarmResponse>(
                AuthenticationError.CurrentTenantRequired());
        }

        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure<ArchivedFarmResponse>(
                AuthenticationError.CurrentUserRequired());
        }

        var access = await effectiveAccessService.CheckTenantAsync(
            actorId,
            tenantId,
            TenantAccessLevel.Owner,
            cancellationToken);

        if (!access.IsAllowed)
        {
            return Result.Failure<ArchivedFarmResponse>(
                FarmError.AccessDenied());
        }

        var farm = await farmQueries.GetArchivedFarmByIdAsync(
            tenantId,
            request.FarmId,
            cancellationToken);

        return farm is null
            ? Result.Failure<ArchivedFarmResponse>(FarmError.NotFound())
            : Result.Success(farm);
    }
}
