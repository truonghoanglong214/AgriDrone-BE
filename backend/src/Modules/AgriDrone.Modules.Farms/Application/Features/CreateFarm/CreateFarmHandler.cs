using AgriDrone.Modules.Farms.Application.Abstractions.Persistence;
using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.Modules.Farms.Application.Provisioning;
using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.SharedInfrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Farms.Application.Features.CreateFarm
{
    internal sealed class CreateFarmHandler(
        IExecutionContext executionContext,
        IEffectiveAccessService effectiveAccessService,
        IFarmProvisioningPort provisioningPort) : IRequestHandler<CreateFarmCommand, Result<CreateFarmResponse>>
    {
        public async Task<Result<CreateFarmResponse>> Handle(CreateFarmCommand request, CancellationToken cancellationToken)
        {
            if (executionContext.TenantId is not Guid tenantId)
                return Result.Failure<CreateFarmResponse>(AuthenticationError.CurrentTenantRequired());

            if (executionContext.ActorId is not Guid userId)
                return Result.Failure<CreateFarmResponse>(AuthenticationError.CurrentUserRequired());

            var access = await effectiveAccessService.CheckTenantAsync(
                userId,
                tenantId,
                TenantAccessLevel.Admin,
                cancellationToken);

            if (!access.IsAllowed)
            {
                return Result.Failure<CreateFarmResponse>(
                    FarmError.AccessDenied());
            }

            var result = await provisioningPort.ProvisionAsync(
                new ProvisionFarmRequest(
                    tenantId,
                    userId,
                    request.code,
                    request.name,
                    request.address,
                    request.boundary,
                    request.centerPoint,
                    request.areaHectares),
                cancellationToken);
            if (result.IsFailure)
            {
                return Result.Failure<CreateFarmResponse>(result.Error);
            }

            return Result.Success(
                new CreateFarmResponse(
                    result.Value.FarmId,
                    result.Value.TenantId,
                    result.Value.Code,
                    result.Value.Name,
                    result.Value.Address,
                    result.Value.Boundary,
                    result.Value.CenterPoint,
                    result.Value.AreaHectares,
                    result.Value.Status,
                    result.Value.CreatedBy,
                    result.Value.CreatedAt));
        }
    }
}
