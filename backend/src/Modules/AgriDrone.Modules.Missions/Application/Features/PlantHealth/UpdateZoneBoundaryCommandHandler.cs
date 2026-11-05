using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.IO;

namespace AgriDrone.Modules.Missions.Application.Features.PlantHealth;

/// <summary>
/// UC-30b: Cap nhat duong bien (polygon) vung benh PROPOSED.
/// Sau khi reshape, tinh lai danh sach cay nam trong polygon moi bang ST_Contains,
/// xoa membership cu va insert membership moi.
/// </summary>
internal sealed class UpdateZoneBoundaryCommandHandler(
    MissionsDbContext dbContext,
    ISystemManagerAccessService managerAccessService,
    ILogger<UpdateZoneBoundaryCommandHandler> logger)
    : IRequestHandler<UpdateZoneBoundaryCommand, Result<UpdateZoneBoundaryResult>>
{
    private sealed record ZoneStatusRow(Guid Id, string Status, int CurrentMembershipVersion);
    private sealed record PlantInZoneRow(Guid PlantId);
    private sealed record GeoJsonRow(string GeoJson);

    public async Task<Result<UpdateZoneBoundaryResult>> Handle(
        UpdateZoneBoundaryCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Auth
        var access = await managerAccessService.ResolveFarmAccessAsync(
            request.FarmId, cancellationToken);
        if (!access.IsAllowed)
        {
            return Result.Failure<UpdateZoneBoundaryResult>(
                AppError.Forbidden("UpdateZoneBoundary.FarmAccessDenied", "Access denied."));
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
            return Result.Failure<UpdateZoneBoundaryResult>(
                AppError.NotFound("UpdateZoneBoundary.ZoneNotFound",
                    $"Zone {request.ZoneId} not found for this farm."));
        }

        var zone = zones[0];
        if (zone.Status != "PROPOSED")
        {
            return Result.Failure<UpdateZoneBoundaryResult>(
                AppError.Conflict("UpdateZoneBoundary.InvalidStatus",
                    $"Zone status is '{zone.Status}'. Only PROPOSED zones can be reshaped."));
        }

        // 3. Serialize geometry moi sang WKT
        var wktWriter = new WKTWriter();
        var geometryWkt = wktWriter.Write(request.NewGeometry);
        var newMembershipVersion = zone.CurrentMembershipVersion + 1;

        // 4. UPDATE sang REVIEWED voi reviewed_geometry da duoc cat theo boundary
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE plant.disease_zones dz
               SET status = 'REVIEWED'::system.disease_zone_status,
                   reviewed_geometry = ST_Intersection(ST_Buffer(fb.geometry, -0.000002), ST_SetSRID(ST_GeomFromText({geometryWkt}), 4326)),
                   current_membership_version = {newMembershipVersion},
                   recommendations_rejected = true,
                   reviewed_by = (SELECT id FROM identity.users LIMIT 1),
                   reviewed_at = NOW(),
                   review_reason = 'Boundary reshaped by manager',
                   review_evidence = '{{}}'::jsonb,
                   updated_at = NOW()
               FROM farm.farm_boundaries fb
               WHERE dz.id = {request.ZoneId} AND fb.id = dz.farm_boundary_version_id",
            cancellationToken);

        // 5. Tim cay nam trong polygon moi bang ST_Contains
        var enclosedPlants = await dbContext.Database.SqlQueryRaw<PlantInZoneRow>(@"
            SELECT p.id AS ""PlantId""
            FROM plant.plants p
            WHERE p.farm_id = {0}
              AND p.lifecycle_status = 'ACTIVE'::system.plant_lifecycle_status
              AND ST_Contains(
                    ST_SetSRID(ST_GeomFromText({1}), 4326),
                    p.location
                  )",
            request.FarmId, geometryWkt)
            .ToListAsync(cancellationToken);

        // 6. Insert snapshot memberships moi cho cay trong polygon (khong DELETE vi membership la snapshot bat bien)
        foreach (var plant in enclosedPlants)
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $@"INSERT INTO plant.disease_zone_memberships (
                    id, disease_zone_id, farm_id, plant_id, membership_version, kind, confidence, created_at
                ) VALUES (
                    {Guid.NewGuid()}, {request.ZoneId}, {request.FarmId}, {plant.PlantId},
                    {newMembershipVersion}, 'PROPOSED'::system.disease_zone_membership_kind, 1.0, NOW()
                )",
                cancellationToken);
        }

        // 8. Lay GeoJSON cua geometry moi de tra ve frontend
        var geoJsonRows = await dbContext.Database.SqlQueryRaw<GeoJsonRow>(@"
            SELECT ST_AsGeoJSON(proposed_geometry) AS ""GeoJson""
            FROM plant.disease_zones
            WHERE id = {0}",
            request.ZoneId)
            .ToListAsync(cancellationToken);

        var geoJson = geoJsonRows.FirstOrDefault()?.GeoJson ?? "{}";

        logger.LogInformation(
            "Zone {ZoneId} boundary updated for farm {FarmId}. Enclosed trees: {Count}",
            request.ZoneId, request.FarmId, enclosedPlants.Count);

        return Result.Success(new UpdateZoneBoundaryResult(
            request.ZoneId,
            geoJson,
            enclosedPlants.Count));
    }
}

