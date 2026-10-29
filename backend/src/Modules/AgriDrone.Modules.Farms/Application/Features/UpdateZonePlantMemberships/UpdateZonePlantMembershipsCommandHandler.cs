using System.Text.Json;
using AgriDrone.Modules.Farms.Application.Abstractions.Persistence;
using AgriDrone.Modules.Farms.Application.Abstractions.Zones;
using AgriDrone.Modules.Farms.Application.Errors;
using AgriDrone.Modules.Farms.Domain.Zones;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using AgriDrone.SharedInfrastructure.Auditing;
using MediatR;

namespace AgriDrone.Modules.Farms.Application.Features.UpdateZonePlantMemberships;

internal sealed class UpdateZonePlantMembershipsCommandHandler(
    IFarmZoneRepository zoneRepository,
    IFarmZonePlantMembershipService membershipService,
    IFarmUnitOfWork unitOfWork,
    ISystemManagerAccessService accessService,
    IExecutionContext executionContext,
    IAuditWriter auditWriter,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateZonePlantMembershipsCommand,
        Result<UpdateZonePlantMembershipsResponse>>
{
    public async Task<Result<UpdateZonePlantMembershipsResponse>> Handle(
        UpdateZonePlantMembershipsCommand command,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
            return Result.Failure<UpdateZonePlantMembershipsResponse>(
                AuthenticationError.CurrentUserRequired());

        var access = await accessService.ResolveFarmAccessAsync(
            command.FarmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId is not Guid tenantId)
            return Result.Failure<UpdateZonePlantMembershipsResponse>(
                FarmZoneError.NotFound());

        var zone = await zoneRepository.GetByIdAsync(
            tenantId, command.FarmId, command.ZoneId, cancellationToken);
        if (zone is null || zone.IsArchived)
            return Result.Failure<UpdateZonePlantMembershipsResponse>(
                FarmZoneError.NotFound());
        if (zone.Version != command.ExpectedZoneVersion)
            return Result.Failure<UpdateZonePlantMembershipsResponse>(
                FarmZoneError.ConcurrentUpdate());

        var changedAt = timeProvider.GetUtcNow();
        var result = await membershipService.ApplyManualAsync(
            command.FarmId,
            command.ZoneId,
            command.Operations,
            actorId,
            command.Reason,
            changedAt,
            cancellationToken);
        if (result.IsFailure)
            return Result.Failure<UpdateZonePlantMembershipsResponse>(result.Error);

        using var auditData = JsonSerializer.SerializeToDocument(new
        {
            command.ExpectedZoneVersion,
            command.Reason,
            command.Operations,
            result.Value.AssignedCount,
            result.Value.UnassignedCount
        });
        auditWriter.AddUserAction(
            unitOfWork,
            tenantId,
            command.FarmId,
            actorId,
            executionContext.CorrelationId,
            nameof(FarmZonePlantMembership),
            command.ZoneId,
            "UPDATE_ZONE_PLANT_MEMBERSHIPS",
            null,
            auditData,
            changedAt);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(new UpdateZonePlantMembershipsResponse(
            command.ZoneId,
            result.Value.AssignedCount,
            result.Value.UnassignedCount));
    }
}
