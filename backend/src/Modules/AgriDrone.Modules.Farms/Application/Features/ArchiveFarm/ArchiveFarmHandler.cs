using System.Text.Json;
using AgriDrone.Modules.Farms.Application.Abstractions.Persistence;
using AgriDrone.Modules.Farms.Application.Abstractions.Queries;
using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.Modules.Farms.Domain.Farms;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Farms.Application.Features.ArchiveFarm;

internal sealed class ArchiveFarmHandler(
    IFarmRepository farmRepository,
    IFarmUnitOfWork unitOfWork,
    IFarmArchiveDependencyQuery dependencyQuery,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    IEffectiveAccessService effectiveAccessService,
    TimeProvider timeProvider)
    : IRequestHandler<ArchiveFarmCommand, Result>
{
    public async Task<Result> Handle(
        ArchiveFarmCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure(
                AuthenticationError.CurrentUserRequired());
        }

        if (executionContext.TenantId is not Guid tenantId)
        {
            return Result.Failure(
                AuthenticationError.CurrentTenantRequired());
        }

        var accessDecision = await effectiveAccessService.CheckTenantAsync(
            actorId,
            tenantId,
            TenantAccessLevel.Owner,
            cancellationToken);

        if (!accessDecision.IsAllowed)
        {
            return Result.Failure(FarmError.AccessDenied());
        }

        var farm = await farmRepository.GetByIdAsync(
            tenantId,
            request.FarmId,
            cancellationToken);

        if (farm is null)
        {
            return Result.Failure(FarmError.NotFound());
        }

        if (farm.Version != request.ExpectedVersion)
        {
            return Result.Failure(FarmError.ConcurrentUpdate());
        }

        var dependencies = await dependencyQuery.GetForFarmAsync(
            tenantId,
            request.FarmId,
            cancellationToken);

        if (dependencies.HasAny)
        {
            return Result.Failure(
                FarmError.ActiveDependenciesExist(
                    dependencies.ActiveZoneCount,
                    dependencies.ActiveMissionCount,
                    dependencies.OpenFieldTaskCount));
        }

        using var oldData = CreateAuditData(farm);

        farmRepository.Update(farm);

        var now = timeProvider.GetUtcNow();
        farm.Archive(now);

        using var newData = CreateAuditData(farm);

        auditWriter.AddUserAction(
            sink: unitOfWork,
            tenantId: tenantId,
            farmId: farm.Id,
            actorId: actorId,
            correlationId: executionContext.CorrelationId,
            entityType: nameof(Farm),
            entityId: farm.Id,
            action: "ARCHIVE",
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
