using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.Modules.Farms.Domain.Zones;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;

namespace AgriDrone.Modules.Farms.Application.Features.GetZoneById;

internal sealed class GetZoneByIdQueryHandler(
    IFarmZoneRepository farmZoneRepository,
    IExecutionContext executionContext,
    IEffectiveAccessService effectiveAccessService)
    : IRequestHandler<GetZoneByIdQuery, Result<GetZoneByIdResponse>>
{
    public async Task<Result<GetZoneByIdResponse>> Handle(
        GetZoneByIdQuery request,
        CancellationToken cancellationToken)
    {
        if (executionContext.TenantId is not Guid tenantId)
        {
            return Result.Failure<GetZoneByIdResponse>(
                AuthenticationError.CurrentTenantRequired());
        }

        if (executionContext.ActorId is not Guid userId)
        {
            return Result.Failure<GetZoneByIdResponse>(
                AuthenticationError.CurrentUserRequired());
        }

        var zone = await farmZoneRepository.GetByIdAsync(
            tenantId,
            request.FarmId,
            request.ZoneId,
            cancellationToken);

        if (zone is null)
        {
            return Result.Failure<GetZoneByIdResponse>(
                FarmZoneError.NotFound());
        }

        var access = await effectiveAccessService.CheckZoneAsync(
            userId,
            tenantId,
            request.FarmId,
            request.ZoneId,
            FarmAccessLevel.Member,
            cancellationToken);

        if (!access.IsAllowed)
        {
            return Result.Failure<GetZoneByIdResponse>(
                FarmZoneError.AccessDenied());
        }

        return Result.Success(new GetZoneByIdResponse(
            zone.Id,
            zone.FarmId,
            zone.Code,
            zone.Name,
            zone.Boundary,
            zone.AreaHectares,
            zone.Status,
            zone.Version,
            zone.CreatedAt,
            zone.CreatedBy,
            zone.UpdatedAt));
    }
}
