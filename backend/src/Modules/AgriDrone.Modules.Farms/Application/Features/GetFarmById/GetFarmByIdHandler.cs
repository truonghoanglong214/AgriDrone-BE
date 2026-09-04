using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Farms.Application.Features.GetFarmById
{
    internal sealed class GetFarmByIdHandler(
        IFarmRepository farmRepository,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        IEffectiveAccessService effectiveAccessService) : IRequestHandler<GetFarmByIdCommand, Result<GetFarmByIdResponse>>
    {
        public async Task<Result<GetFarmByIdResponse>> Handle(GetFarmByIdCommand request, CancellationToken cancellationToken)
        {
            if(currentTenant.TenantId is not Guid tenantId)
            {
                return Result.Failure<GetFarmByIdResponse>(AuthenticationError.CurrentTenantRequired());
            }

            if (currentUser.UserId is not Guid userId)
            {
                return Result.Failure<GetFarmByIdResponse>(
                    AuthenticationError.CurrentUserRequired());
            }

            var farm = await farmRepository.GetByIdAsync(tenantId, request.FarmId, cancellationToken);

            if (farm is null)
            {
                return Result.Failure<GetFarmByIdResponse>(FarmError.NotFound());
            }

            var access = await effectiveAccessService.CheckFarmAsync(
                userId,
                tenantId,
                farm.Id,
                FarmAccessLevel.Member,
                cancellationToken);

            if (!access.IsAllowed)
            {
                return Result.Failure<GetFarmByIdResponse>(FarmError.AccessDenied());
            }

            return Result.Success(new GetFarmByIdResponse(
                farm.Id,
                farm.TenantId,
                farm.Code,
                farm.Name,
                farm.Address,
                farm.Boundary,
                farm.CenterPoint,
                farm.AreaHectares,
                farm.Status,
                farm.CreatedAt,
                farm.CreatedBy));

        }
    }
}
