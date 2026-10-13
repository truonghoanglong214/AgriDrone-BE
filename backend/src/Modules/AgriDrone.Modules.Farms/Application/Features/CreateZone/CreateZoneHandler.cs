using AgriDrone.Modules.Farms.Application.Abstractions.Persistence;
using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.Modules.Farms.Application.Policies;
using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.Modules.Farms.Domain.Zones;
using AgriDrone.SharedInfrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedKernel.Domain;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Farms.Application.Features.CreateZone
{
    internal sealed class CreateZoneHandler(
        IFarmZoneRepository farmZoneRepository,
        IFarmRepository farmRepository,
        IFarmUnitOfWork unitOfWork,
        IExecutionContext executionContext,
        ISystemManagerAccessService managerAccessService,
        IFarmGeometryPolicy geometryPolicy,
        TimeProvider timeProvider) : IRequestHandler<CreateZoneCommand, Result<CreateZoneResponse>>
    {
        private const string ActiveZoneCodeConstraint =
            "ux_farm_zones_farm_code_active";

        public async Task<Result<CreateZoneResponse>> Handle(CreateZoneCommand request, CancellationToken cancellationToken)
        {
            if (executionContext.ActorId is not Guid userId)
                return Result.Failure<CreateZoneResponse>(AuthenticationError.CurrentUserRequired());

            var access = await managerAccessService.ResolveFarmAccessAsync(
                request.FarmId,
                cancellationToken);

            if (!access.IsAllowed || access.TenantId is not Guid tenantId)
                return Result.Failure<CreateZoneResponse>(FarmError.AccessDenied());

            var farm = await farmRepository.GetByIdAsync(
                tenantId,
                request.FarmId,
                cancellationToken);

            if (farm is null)
                return Result.Failure<CreateZoneResponse>(FarmError.NotFound());

            var geometryDecision = await geometryPolicy.ValidateZoneAsync(
                tenantId,
                farm,
                request.Boundary,
                request.AreaHectares,
                cancellationToken: cancellationToken);
            if (geometryDecision.IsFailure)
            {
                return Result.Failure<CreateZoneResponse>(geometryDecision.Error);
            }

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

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception)
                when (exception.IsUniqueConstraintViolation(
                    ActiveZoneCodeConstraint))
            {
                return Result.Failure<CreateZoneResponse>(
                    FarmZoneError.CodeAlreadyExists(normalizedCode));
            }

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
