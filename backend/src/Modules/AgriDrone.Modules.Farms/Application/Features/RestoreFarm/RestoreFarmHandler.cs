using AgriDrone.Modules.Farms.Application.Abstractions.Persistence;
using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Text.Json;

namespace AgriDrone.Modules.Farms.Application.Features.RestoreFarm
{
    internal sealed class RestoreFarmHandler(
        IFarmRepository farmRepository,
        IFarmUnitOfWork unitOfWork,
        IEffectiveAccessService effectiveAccessService,
        IExecutionContext executionContext,
        IAuditWriter auditWriter,
        TimeProvider timeProvider
        ) : IRequestHandler<RestoreFarmCommand, Result>
    {
        public async Task<Result> Handle(RestoreFarmCommand request, CancellationToken cancellationToken)
        {
            if(executionContext.TenantId is not Guid tenantId)
                return Result.Failure(AuthenticationError.CurrentTenantRequired());

            if(executionContext.ActorId is not Guid actorId)
                return Result.Failure(AuthenticationError.CurrentUserRequired());

            var hasAccess = await effectiveAccessService.CheckTenantAsync(
                actorId,
                tenantId, 
                TenantAccessLevel.Owner, 
                cancellationToken);

            if (!hasAccess.IsAllowed)
                return Result.Failure(FarmError.AccessDenied());

            var farm = await farmRepository.GetByIdIncludingArchivedAsync(
                tenantId,
                request.FarmId,
                cancellationToken);

            if (farm is null)
                return Result.Failure(FarmError.NotFound());

            if(farm.Version != request.ExpectedVersion)
                return Result.Failure(FarmError.ConcurrentUpdate());

            var isCodeExisting = await farmRepository.ActiveCodeExistsAsync(
                tenantId,
                farm.Code,
                excludingFarmId: farm.Id,
                cancellationToken);

            if (isCodeExisting)
                return Result.Failure(FarmError.CodeAlreadyExists(farm.Code));

            using var oldData = CreateAuditData(farm);
            farmRepository.Update(farm);

            var now = timeProvider.GetUtcNow();
            farm.Restore(now);

            using var newData = CreateAuditData(farm);

            auditWriter.AddUserAction(
                sink: unitOfWork,
                tenantId: tenantId,
                farmId: farm.Id,
                actorId: actorId,
                correlationId: executionContext.CorrelationId,
                entityType: nameof(Farm),
                entityId: farm.Id,
                action: "RESTORE",
                oldData: oldData,
                newData: newData,
                createdAt: now);

            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken);

            }
            catch (DbUpdateConcurrencyException)
            {
                return Result.Failure(FarmError.ConcurrentUpdate());
            }

            return Result.Success();
        }

        private static JsonDocument CreateAuditData(Farm farm) =>
        JsonSerializer.SerializeToDocument(new
        {
            Status = farm.Status.ToString(),
            farm.DeletedAt,
            farm.UpdatedAt,
            farm.Version
        });
    }
}
