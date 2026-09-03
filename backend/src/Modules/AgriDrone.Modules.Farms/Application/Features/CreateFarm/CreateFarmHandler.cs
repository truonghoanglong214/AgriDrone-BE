using AgriDrone.Modules.Farms.Application.Abstractions.Persistence;
using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions;
using AgriDrone.SharedKernel.Domain;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Farms.Application.Features.CreateFarm
{
    internal sealed class CreateFarmHandler(
        IFarmRepository farmRepository,
        ICurrentTenant currentTenant,
        ICurrentUser currentUser,
        IFarmUnitOfWork unitOfWork,
        TimeProvider timeProvider) : IRequestHandler<CreateFarmCommand, Result<CreateFarmResponse>>
    {
        public async Task<Result<CreateFarmResponse>> Handle(CreateFarmCommand request, CancellationToken cancellationToken)
        {
            if (currentTenant.TenantId is not Guid tenantId)
                return Result.Failure<CreateFarmResponse>(AuthenticationError.CurrentTenantRequired());

            if (currentUser.UserId is not Guid userId)
                return Result.Failure<CreateFarmResponse>(AuthenticationError.CurrentUserRequired());

            var now = timeProvider.GetUtcNow();
            var normalizedCode = request.code.ToUpperInvariant();
            var existingCode = await farmRepository.GetByCodeAsync(tenantId, normalizedCode, cancellationToken);

            if (existingCode is not null)
                return Result.Failure <CreateFarmResponse>(FarmError.CodeAlreadyExists(existingCode.Code));

            var newFarm = Farm.Create(
                tenantId,
                normalizedCode,
                request.name,
                request.address,
                request.boundary,
                request.centerPoint,
                request.areaHectares,
                GeneralStatus.Active,
                userId,
                now);

            farmRepository.Add(newFarm);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(
                new CreateFarmResponse(
                    newFarm.Id,
                    newFarm.TenantId,
                    newFarm.Code,
                    newFarm.Name,
                    newFarm.Address,
                    newFarm.Boundary,
                    newFarm.CenterPoint,
                    newFarm.AreaHectares,
                    newFarm.Status,
                    newFarm.CreatedBy,
                    newFarm.CreatedAt));
        }
    }
}
