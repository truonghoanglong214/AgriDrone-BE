using AgriDrone.Modules.Farms.Application.Abstractions.Zones;
using AgriDrone.Modules.Farms.Domain.Zones;
using AgriDrone.Modules.Farms.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Farms.Infrastructure.Zones;

internal sealed class FarmZonePlantMembershipService(FarmsDbContext dbContext)
    : IFarmZonePlantMembershipService
{
    public async Task<int> RecomputeAutoAsync(
        Guid farmId,
        Guid zoneId,
        Polygon boundary,
        Guid actorId,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(boundary);

        var targetPlantIds = (await dbContext.Database.SqlQueryRaw<Guid>(
                "SELECT plant_row.id AS \"Value\" FROM plant.plants AS plant_row " +
                "WHERE plant_row.farm_id = {0} AND plant_row.location IS NOT NULL " +
                "AND plant_row.lifecycle_status = 'ACTIVE'::system.plant_lifecycle_status " +
                "AND ST_Covers({1}, plant_row.location)",
                farmId,
                boundary)
            .ToListAsync(cancellationToken)).ToHashSet();

        var activeMemberships = await dbContext.FarmZonePlantMemberships
            .Where(membership => membership.FarmId == farmId &&
                                 membership.EffectiveTo == null)
            .ToListAsync(cancellationToken);
        var activeByPlant = activeMemberships.ToDictionary(membership => membership.PlantId);
        var changed = 0;

        foreach (var membership in activeMemberships.Where(membership =>
                     membership.ZoneId == zoneId &&
                     membership.Source == FarmZonePlantMembershipSource.AutoGeometry &&
                     !targetPlantIds.Contains(membership.PlantId)))
        {
            membership.Close(changedAt, "Zone boundary recomputed");
            changed++;
        }

        foreach (var plantId in targetPlantIds)
        {
            if (activeByPlant.TryGetValue(plantId, out var existing))
            {
                if (existing.Source == FarmZonePlantMembershipSource.ManualOverride ||
                    existing.ZoneId == zoneId)
                {
                    continue;
                }

                existing.Close(changedAt, "Reassigned by zone geometry");
            }

            dbContext.FarmZonePlantMemberships.Add(
                FarmZonePlantMembership.Create(
                    farmId,
                    zoneId,
                    plantId,
                    FarmZonePlantMembershipSource.AutoGeometry,
                    actorId,
                    "Assigned from zone boundary",
                    changedAt));
            changed++;
        }

        return changed;
    }

    public async Task<int> CloseZoneAsync(
        Guid farmId,
        Guid zoneId,
        Guid actorId,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken)
    {
        var memberships = await dbContext.FarmZonePlantMemberships
            .Where(membership => membership.FarmId == farmId &&
                                 membership.ZoneId == zoneId &&
                                 membership.EffectiveTo == null)
            .ToListAsync(cancellationToken);
        foreach (var membership in memberships)
        {
            membership.Close(changedAt, $"Zone archived by {actorId:D}");
        }

        return memberships.Count;
    }

    public async Task<Result<ZonePlantMembershipChangeResult>> ApplyManualAsync(
        Guid farmId,
        Guid zoneId,
        IReadOnlyList<ZonePlantMembershipOperation> operations,
        Guid actorId,
        string reason,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken)
    {
        if (operations.Count == 0 || string.IsNullOrWhiteSpace(reason))
        {
            return Result.Failure<ZonePlantMembershipChangeResult>(
                AppError.Validation("FarmZonePlantMembership.InvalidRequest",
                    "At least one operation and a reason are required."));
        }

        var duplicatePlantId = operations.GroupBy(operation => operation.PlantId)
            .FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicatePlantId.HasValue || operations.Any(operation => operation.PlantId == Guid.Empty))
        {
            return Result.Failure<ZonePlantMembershipChangeResult>(
                AppError.Validation("FarmZonePlantMembership.DuplicatePlant",
                    "Each non-empty PlantId may appear only once."));
        }

        var requestedIds = operations.Select(operation => operation.PlantId).ToArray();
        var farmPlantIds = (await dbContext.Database.SqlQueryRaw<Guid>(
                "SELECT plant_row.id AS \"Value\" FROM plant.plants AS plant_row " +
                "WHERE plant_row.farm_id = {0} AND plant_row.id = ANY({1}) " +
                "AND plant_row.lifecycle_status = 'ACTIVE'::system.plant_lifecycle_status",
                farmId,
                requestedIds)
            .ToListAsync(cancellationToken)).ToHashSet();
        if (farmPlantIds.Count != requestedIds.Length)
        {
            return Result.Failure<ZonePlantMembershipChangeResult>(
                AppError.NotFound("FarmZonePlantMembership.PlantNotFound",
                    "One or more active Plants do not belong to the Farm."));
        }

        var activeMemberships = await dbContext.FarmZonePlantMemberships
            .Where(membership => requestedIds.Contains(membership.PlantId) &&
                                 membership.EffectiveTo == null)
            .ToListAsync(cancellationToken);
        var activeByPlant = activeMemberships.ToDictionary(membership => membership.PlantId);
        var assigned = 0;
        var unassigned = 0;

        foreach (var operation in operations)
        {
            activeByPlant.TryGetValue(operation.PlantId, out var existing);
            if (operation.Action == ZonePlantMembershipAction.Unassign)
            {
                if (existing is not null)
                {
                    existing.Close(changedAt, reason);
                }
                dbContext.FarmZonePlantMemberships.Add(
                    FarmZonePlantMembership.Create(
                        farmId,
                        zoneId,
                        operation.PlantId,
                        FarmZonePlantMembershipSource.ManualOverride,
                        actorId,
                        reason,
                        changedAt,
                        isAssigned: false));
                unassigned++;
                continue;
            }

            if (existing is not null)
            {
                existing.Close(changedAt, reason);
            }

            dbContext.FarmZonePlantMemberships.Add(
                FarmZonePlantMembership.Create(
                    farmId,
                    zoneId,
                    operation.PlantId,
                    FarmZonePlantMembershipSource.ManualOverride,
                    actorId,
                    reason,
                    changedAt));
            assigned++;
        }

        return Result.Success(new ZonePlantMembershipChangeResult(assigned, unassigned));
    }
}
