using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgriDrone.Modules.Missions.Application.Features.PlantHealth;

/// <summary>
/// UC-30c: Them/xoa cay thanh vien khoi vung benh PROPOSED theo thao tac thu cong.
/// Bump current_membership_version sau moi thay doi.
/// </summary>
internal sealed class UpdateZoneMembershipsCommandHandler(
    MissionsDbContext dbContext,
    ISystemManagerAccessService managerAccessService,
    ILogger<UpdateZoneMembershipsCommandHandler> logger)
    : IRequestHandler<UpdateZoneMembershipsCommand, Result<UpdateZoneMembershipsResult>>
{
    private sealed record ZoneStatusRow(Guid Id, string Status, int CurrentMembershipVersion);
    private sealed record PlantValidRow(Guid Id);
    private sealed record CountRow(int Value);

    public async Task<Result<UpdateZoneMembershipsResult>> Handle(
        UpdateZoneMembershipsCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Auth
        var access = await managerAccessService.ResolveFarmAccessAsync(
            request.FarmId, cancellationToken);
        if (!access.IsAllowed)
        {
            return Result.Failure<UpdateZoneMembershipsResult>(
                AppError.Forbidden("UpdateZoneMemberships.FarmAccessDenied", "Access denied."));
        }

        // 2. Lay zone
        var zones = await dbContext.Database.SqlQueryRaw<ZoneStatusRow>(@"
            SELECT id AS ""Id"", status::text AS ""Status"",
                   current_membership_version AS ""CurrentMembershipVersion""
            FROM plant.disease_zones
            WHERE id = {0} AND farm_id = {1}",
            request.ZoneId, request.FarmId)
            .ToListAsync(cancellationToken);

        if (zones.Count == 0)
        {
            return Result.Failure<UpdateZoneMembershipsResult>(
                AppError.NotFound("UpdateZoneMemberships.ZoneNotFound",
                    $"Zone {request.ZoneId} not found for this farm."));
        }

        var zone = zones[0];
        if (zone.Status != "PROPOSED")
        {
            return Result.Failure<UpdateZoneMembershipsResult>(
                AppError.Conflict("UpdateZoneMemberships.InvalidStatus",
                    $"Zone status is '{zone.Status}'. Only PROPOSED zones can be edited."));
        }

        var addedCount = 0;
        var removedCount = 0;
        var newVersion = zone.CurrentMembershipVersion + 1;

        // 3. Lay danh sach plantId hien tai o version hien tai
        var currentMembers = await dbContext.Database.SqlQueryRaw<PlantValidRow>(@"
            SELECT plant_id AS ""Id""
            FROM plant.disease_zone_memberships
            WHERE disease_zone_id = {0} AND membership_version = {1}",
            request.ZoneId, zone.CurrentMembershipVersion)
            .ToListAsync(cancellationToken);

        var memberSet = new HashSet<Guid>(currentMembers.Select(m => m.Id));

        // 4. Xoa cay khoi set (khong dung DELETE SQL vi membership la snapshot bat bien)
        foreach (var plantId in request.RemovePlantIds)
        {
            if (memberSet.Remove(plantId))
            {
                removedCount++;
            }
        }

        // 5. Validate & them cay moi vao set
        foreach (var plantId in request.AddPlantIds)
        {
            // Validate cay thuoc farm va ACTIVE
            var validRows = await dbContext.Database.SqlQueryRaw<PlantValidRow>(@"
                SELECT id AS ""Id"" FROM plant.plants
                WHERE id = {0} AND farm_id = {1}
                  AND lifecycle_status = 'ACTIVE'::system.plant_lifecycle_status",
                plantId, request.FarmId)
                .ToListAsync(cancellationToken);

            if (validRows.Count == 0)
            {
                return Result.Failure<UpdateZoneMembershipsResult>(
                    AppError.NotFound("UpdateZoneMemberships.PlantNotFound",
                        $"Plant {plantId} not found or inactive in this farm."));
            }

            if (memberSet.Add(plantId))
            {
                addedCount++;
            }
        }

        // 6. Neu co thay doi, insert snapshot moi vao newVersion va bump version tren zone
        if (addedCount > 0 || removedCount > 0)
        {
            foreach (var plantId in memberSet)
            {
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $@"INSERT INTO plant.disease_zone_memberships (
                        id, disease_zone_id, farm_id, plant_id, membership_version, kind, confidence, created_at
                    ) VALUES (
                        {Guid.NewGuid()}, {request.ZoneId}, {request.FarmId}, {plantId},
                        {newVersion}, 'PROPOSED'::system.disease_zone_membership_kind, 1.0, NOW()
                    )",
                    cancellationToken);
            }

            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $@"UPDATE plant.disease_zones dz
                   SET status = 'REVIEWED'::system.disease_zone_status,
                       reviewed_geometry = ST_Intersection(ST_Buffer(fb.geometry, -0.000002), dz.proposed_geometry),
                       current_membership_version = {newVersion},
                       recommendations_rejected = true,
                       reviewed_by = (SELECT id FROM identity.users LIMIT 1),
                       reviewed_at = NOW(),
                       review_reason = 'Memberships updated by manager',
                       review_evidence = '{{}}'::jsonb,
                       updated_at = NOW()
                   FROM farm.farm_boundaries fb
                   WHERE dz.id = {request.ZoneId} AND fb.id = dz.farm_boundary_version_id AND dz.status = 'PROPOSED'::system.disease_zone_status",
                cancellationToken);
        }

        // 7. Tinh tong so cay hien tai trong zone o version moi nhat
        var activeVersion = (addedCount > 0 || removedCount > 0) ? newVersion : zone.CurrentMembershipVersion;
        var totalRows = await dbContext.Database.SqlQueryRaw<CountRow>(@"
            SELECT COUNT(*)::int AS ""Value""
            FROM plant.disease_zone_memberships
            WHERE disease_zone_id = {0} AND membership_version = {1}",
            request.ZoneId, activeVersion)
            .FirstOrDefaultAsync(cancellationToken);

        var totalTrees = totalRows?.Value ?? 0;

        logger.LogInformation(
            "Zone {ZoneId} memberships updated: +{Added} -{Removed}, total={Total}",
            request.ZoneId, addedCount, removedCount, totalTrees);

        return Result.Success(new UpdateZoneMembershipsResult(
            request.ZoneId,
            totalTrees,
            addedCount,
            removedCount));
    }
}

