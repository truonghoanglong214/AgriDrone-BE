using System.Text.Json;
using AgriDrone.Modules.Farms.Application.Abstractions.Persistence;
using AgriDrone.Modules.Farms.Application.Abstractions.Queries;
using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.Modules.Farms.Domain.Zones;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Farms.Application.Features.ArchiveZone;

internal sealed class ArchiveZoneHandler(
    IFarmZoneRepository farmZoneRepository,
    IFarmUnitOfWork unitOfWork,
    IFarmArchiveDependencyQuery dependencyQuery,
    IAuditWriter auditWriter,
    IExecutionContext executionContext,
    ISystemManagerAccessService managerAccessService,
    TimeProvider timeProvider)
    : IRequestHandler<ArchiveZoneCommand, Result>
{
    public async Task<Result> Handle(
        ArchiveZoneCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
        {
            return Result.Failure(
                AuthenticationError.CurrentUserRequired());
        }

        var accessDecision = await managerAccessService.ResolveFarmAccessAsync(
            request.FarmId,
            cancellationToken);

        if (!accessDecision.IsAllowed || accessDecision.TenantId is not Guid tenantId)
        {
            return Result.Failure(FarmZoneError.AccessDenied());
        }

        var zone = await farmZoneRepository.GetByIdAsync(
            tenantId,
            request.FarmId,
            request.ZoneId,
            cancellationToken);

        if (zone is null)
        {
            return Result.Failure(FarmZoneError.NotFound());
        }

        if (zone.Version != request.ExpectedVersion)
        {
            return Result.Failure(FarmZoneError.ConcurrentUpdate());
        }

        var dependencies = await dependencyQuery.GetForZoneAsync(
            tenantId,
            request.FarmId,
            request.ZoneId,
            cancellationToken);

        if (dependencies.HasAny)
        {
            return Result.Failure(
                FarmZoneError.ActiveDependenciesExist(
                    dependencies.ActiveMissionCount));
        }

        using var oldData = CreateAuditData(zone);

        farmZoneRepository.Update(zone);

        var now = timeProvider.GetUtcNow();
        zone.Archive(now);

        using var newData = CreateAuditData(zone);

        auditWriter.AddUserAction(
            sink: unitOfWork,
            tenantId: tenantId,
            farmId: request.FarmId,
            actorId: actorId,
            correlationId: executionContext.CorrelationId,
            entityType: nameof(FarmZone),
            entityId: zone.Id,
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
            return Result.Failure(FarmZoneError.ConcurrentUpdate());
        }

        return Result.Success();
    }

    private static JsonDocument CreateAuditData(FarmZone zone) =>
        JsonSerializer.SerializeToDocument(new
        {
            Status = zone.Status.ToString(),
            zone.DeletedAt,
            zone.UpdatedAt,
            zone.Version
        });
}
