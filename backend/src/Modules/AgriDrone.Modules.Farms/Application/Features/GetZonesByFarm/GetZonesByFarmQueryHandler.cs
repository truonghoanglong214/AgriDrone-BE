using AgriDrone.Modules.Farms.Application.Abstractions.Queries;
using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using MediatR;

namespace AgriDrone.Modules.Farms.Application.Features.GetZonesByFarm;

internal sealed class GetZonesByFarmQueryHandler(
    IFarmRepository farmRepository,
    IFarmZoneQueries farmZoneQueries,
    ICurrentTenant currentTenant,
    ICurrentUser currentUser,
    IEffectiveAccessService effectiveAccessService)
    : IRequestHandler<
        GetZonesByFarmQuery,
        Result<IReadOnlyList<ZoneListItemResponse>>>
{
    public async Task<Result<IReadOnlyList<ZoneListItemResponse>>> Handle(
        GetZonesByFarmQuery request,
        CancellationToken cancellationToken)
    {
        if (currentTenant.TenantId is not Guid tenantId)
        {
            return Result.Failure<IReadOnlyList<ZoneListItemResponse>>(
                AuthenticationError.CurrentTenantRequired());
        }

        if (currentUser.UserId is not Guid userId)
        {
            return Result.Failure<IReadOnlyList<ZoneListItemResponse>>(
                AuthenticationError.CurrentUserRequired());
        }

        var farm = await farmRepository.GetByIdAsync(
            tenantId,
            request.FarmId,
            cancellationToken);

        if (farm is null)
        {
            return Result.Failure<IReadOnlyList<ZoneListItemResponse>>(
                FarmError.NotFound());
        }

        var access = await effectiveAccessService.CheckFarmAsync(
            userId,
            tenantId,
            request.FarmId,
            FarmAccessLevel.Member,
            cancellationToken);

        if (!access.IsAllowed)
        {
            return Result.Failure<IReadOnlyList<ZoneListItemResponse>>(
                FarmError.AccessDenied());
        }

        var zones = await farmZoneQueries.GetByFarmAsync(
            tenantId,
            request.FarmId,
            cancellationToken);

        var accessibleZones = new List<ZoneListItemResponse>(zones.Count);

        foreach (var zone in zones)
        {
            var zoneAccess = await effectiveAccessService.CheckZoneAsync(
                userId,
                tenantId,
                request.FarmId,
                zone.ZoneId,
                FarmAccessLevel.Member,
                cancellationToken);

            if (zoneAccess.IsAllowed)
            {
                accessibleZones.Add(zone);
            }
        }

        return Result.Success<IReadOnlyList<ZoneListItemResponse>>(
            accessibleZones);
    }
}
