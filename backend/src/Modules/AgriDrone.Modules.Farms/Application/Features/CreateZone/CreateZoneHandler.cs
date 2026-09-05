using AgriDrone.Modules.Farms.Application.Abstractions.Persistence;
using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Farms.Domain.Zones;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Domain;
using MediatR;
using System;
using System.Collections.Generic;
using System.Text;

namespace AgriDrone.Modules.Farms.Application.Features.CreateZone
{
    internal sealed class CreateZoneHandler(
        IFarmZoneRepository farmZoneRepository,
        IFarmRepository farmRepository,
        IFarmUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        ICurrentTenant currentTenant,
        IEffectiveAccessService effectiveAccessService,
        TimeProvider timeProvider) : IRequestHandler<CreateZoneCommand, Result<CreateZoneResponse>>
    {
        public async Task<Result<CreateZoneResponse>> Handle(CreateZoneCommand request, CancellationToken cancellationToken)
        {
            if (currentUser.UserId is not Guid userId)
                return Result.Failure<CreateZoneResponse>(AuthenticationError.CurrentUserRequired());

            if (currentTenant.TenantId is not Guid tenantId)
                return Result.Failure<CreateZoneResponse>(AuthenticationError.CurrentTenantRequired());

            var access = await effectiveAccessService.CheckFarmAsync(
                userId, 
                tenantId, 
                request.FarmId, 
                FarmAccessLevel.Manager, 
                cancellationToken);

            if (!access.IsAllowed)
                return Result.Failure<CreateZoneResponse>(FarmError.AccessDenied());

            var farm = await farmRepository.GetByIdAsync(
                tenantId,
                request.FarmId,
                cancellationToken);

            if (farm is null)
                return Result.Failure<CreateZoneResponse>(FarmError.NotFound());

            var now = timeProvider.GetUtcNow();
            var normalizedCode = request.Code.Trim().ToUpperInvariant();
            var name = request.Name.Trim();
            var codeExists = await farmZoneRepository.ActiveCodeExistsAsync(
                tenantId,
                request.FarmId,
                normalizedCode,
                cancellationToken: cancellationToken);

            if (codeExists)
                return Result.Failure<CreateZoneResponse>(
                    FarmZoneError.CodeAlreadyExists(normalizedCode));

            var newZone = FarmZone.Create(
                request.FarmId,
                normalizedCode,
                name,
                request.Boundary,
                request.AreaHectares,
                GeneralStatus.Active,
                userId,
                now);
            
            farmZoneRepository.Add(newZone);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(
                new CreateZoneResponse(
                    newZone.Id,
                    newZone.FarmId,
                    newZone.Code,
                    newZone.Name,
                    newZone.Boundary,
                    newZone.AreaHectares,
                    newZone.Status,
                    newZone.Version,
                    newZone.CreatedAt,
                    newZone.CreatedBy));
        }
    }
}
