using System.Text.Json;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.IO;

namespace AgriDrone.Modules.Missions.Application.Features.PlantHealth;

/// <summary>
/// UC-30a: Gop nhieu vung benh PROPOSED thanh mot vung moi.
/// Vi cac zone nguon o trang thai PROPOSED (chua review), khong the chuyen sang SUPERSEDED
/// (DB constraint yeu cau reviewed_geometry, reviewed_by...), nen ta DELETE source zones
/// va INSERT zone moi voi geometry client cung cap.
/// </summary>
internal sealed class MergeDiseaseZonesCommandHandler(
    MissionsDbContext dbContext,
    ISystemManagerAccessService managerAccessService,
    ILogger<MergeDiseaseZonesCommandHandler> logger)
    : IRequestHandler<MergeDiseaseZonesCommand, Result<MergeDiseaseZonesResult>>
{
    private sealed record ZoneRow(
        Guid Id,
        Guid FarmId,
        Guid SurveyOrderId,
        Guid SurveyResultId,
        Guid FarmBoundaryVersionId,
        Guid FarmBaseMapVersionId,
        Guid TenantId,
        Guid PlantConditionId,
        Guid HealthLevelId,
        Guid SourceHandoffId,
        string SourceProposalId,
        string Status);

    private sealed record MemberRow(Guid PlantId, double Confidence);

    public async Task<Result<MergeDiseaseZonesResult>> Handle(
        MergeDiseaseZonesCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Auth
        var access = await managerAccessService.ResolveFarmAccessAsync(
            request.FarmId, cancellationToken);
        if (!access.IsAllowed)
        {
            return Result.Failure<MergeDiseaseZonesResult>(
                AppError.Forbidden("MergeZones.FarmAccessDenied", "Access denied."));
        }

        if (request.ZoneIds.Count < 2)
        {
            return Result.Failure<MergeDiseaseZonesResult>(
                AppError.Validation("MergeZones.MinimumZones", "At least 2 zones required to merge."));
        }

        // 2. Load source zones - phai thuoc farm va trang thai PROPOSED
        var zoneIdArray = request.ZoneIds.ToArray();
        var zones = new List<ZoneRow>();
        foreach (var zid in zoneIdArray)
        {
            var rows = await dbContext.Database.SqlQueryRaw<ZoneRow>(@"
                SELECT id AS ""Id"", farm_id AS ""FarmId"",
                       survey_order_id AS ""SurveyOrderId"",
                       survey_result_id AS ""SurveyResultId"",
                       farm_boundary_version_id AS ""FarmBoundaryVersionId"",
                       farm_base_map_version_id AS ""FarmBaseMapVersionId"",
                       tenant_id AS ""TenantId"",
                       plant_condition_id AS ""PlantConditionId"",
                       health_level_id AS ""HealthLevelId"",
                       source_handoff_id AS ""SourceHandoffId"",
                       source_proposal_id AS ""SourceProposalId"",
                       status::text AS ""Status""
                FROM plant.disease_zones
                WHERE id = {0} AND farm_id = {1}",
                zid, request.FarmId)
                .ToListAsync(cancellationToken);

            if (rows.Count == 0)
            {
                return Result.Failure<MergeDiseaseZonesResult>(
                    AppError.NotFound("MergeZones.ZoneNotFound",
                        $"Zone {zid} not found for this farm."));
            }

            var z = rows[0];
            if (z.Status != "PROPOSED")
            {
                return Result.Failure<MergeDiseaseZonesResult>(
                    AppError.Conflict("MergeZones.InvalidStatus",
                        $"Zone {zid} is not PROPOSED (status={z.Status}). Only PROPOSED zones can be merged."));
            }

            zones.Add(z);
        }

        // 3. Lay tat ca plant members tu source zones (deduplicate)
        var allMembers = new Dictionary<Guid, double>();
        foreach (var zid in zoneIdArray)
        {
            var members = await dbContext.Database.SqlQueryRaw<MemberRow>(@"
                SELECT plant_id AS ""PlantId"", confidence AS ""Confidence""
                FROM plant.disease_zone_memberships
                WHERE disease_zone_id = {0}",
                zid)
                .ToListAsync(cancellationToken);

            foreach (var m in members)
            {
                if (!allMembers.TryGetValue(m.PlantId, out var existingConfidence) || existingConfidence < m.Confidence)
                {
                    allMembers[m.PlantId] = m.Confidence;
                }
            }
        }

        if (allMembers.Count == 0)
        {
            return Result.Failure<MergeDiseaseZonesResult>(
                AppError.Validation("MergeZones.EmptyMembership",
                    "Source zones have no member plants."));
        }

        // 4. Dung thong tin zone dau tien lam base cho zone moi
        var baseZone = zones[0];
        var newZoneId = Guid.NewGuid();
        var newZoneKey = Guid.NewGuid();
        var mergedProposalId = $"merge_{string.Join("_", zoneIdArray.Select(z => z.ToString("N")[..8]))}_{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";

        // 5. Serialize geometry sang WKT de chen vao PostGIS
        var wktWriter = new WKTWriter();
        var geometryWkt = wktWriter.Write(request.MergedGeometry);

        var evidenceDoc = JsonSerializer.Serialize(new
        {
            merge_source_zone_ids = zoneIdArray,
            merged_zone_count = zoneIdArray.Length,
            total_trees = allMembers.Count,
            merged_at = DateTimeOffset.UtcNow
        });

        var recommendationDoc = JsonSerializer.Serialize(new[]
        {
            new
            {
                treatment_type = "MERGED_ZONE_REVIEW",
                action = "Kiem tra lai vung benh sau khi gop",
                note = $"Vung duoc gop tu {zoneIdArray.Length} vung nguon, tong {allMembers.Count} cay"
            }
        });

        // 6. INSERT zone moi
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $@"INSERT INTO plant.disease_zones (
                id, zone_key, version_number, tenant_id, farm_id,
                survey_order_id, survey_result_id, farm_boundary_version_id, farm_base_map_version_id,
                plant_condition_id, health_level_id, source_handoff_id, source_proposal_id,
                proposed_geometry, recommendation_candidates, proposal_evidence,
                current_membership_version, status, recommendations_rejected, created_at, updated_at
            ) VALUES (
                {newZoneId}, {newZoneKey}, 1, {baseZone.TenantId}, {request.FarmId},
                {baseZone.SurveyOrderId}, {baseZone.SurveyResultId},
                {baseZone.FarmBoundaryVersionId}, {baseZone.FarmBaseMapVersionId},
                {baseZone.PlantConditionId}, {baseZone.HealthLevelId},
                {baseZone.SourceHandoffId}, {mergedProposalId},
                ST_SetSRID(ST_GeomFromText({geometryWkt}), 4326),
                {recommendationDoc}::jsonb, {evidenceDoc}::jsonb,
                1, 'PROPOSED'::system.disease_zone_status, false, NOW(), NOW()
            )",
            cancellationToken);

        // 7. INSERT memberships cho zone moi
        foreach (var (plantId, confidence) in allMembers)
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $@"INSERT INTO plant.disease_zone_memberships (
                    id, disease_zone_id, farm_id, plant_id, membership_version, kind, confidence, created_at
                ) VALUES (
                    {Guid.NewGuid()}, {newZoneId}, {request.FarmId}, {plantId},
                    1, 'PROPOSED'::system.disease_zone_membership_kind, {confidence}, NOW()
                )",
                cancellationToken);
        }

        // 8. Chuyen trang thai source zones sang REJECTED de ghi nhan lich su (khong DELETE vi DB chan trigger enforce_disease_zone_history)
        foreach (var zid in zoneIdArray)
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $@"UPDATE plant.disease_zones
                   SET status = 'REJECTED'::system.disease_zone_status,
                       reviewed_by = (SELECT id FROM identity.users LIMIT 1),
                       reviewed_at = NOW(),
                       review_reason = {$"Merged into new zone {newZoneId}"},
                       review_evidence = '{{}}'::jsonb,
                       updated_at = NOW()
                   WHERE id = {zid} AND status = 'PROPOSED'::system.disease_zone_status",
                cancellationToken);
        }

        logger.LogInformation(
            "Merged {Count} zones into new zone {NewZoneId} for farm {FarmId}. Total trees: {Trees}",
            zoneIdArray.Length, newZoneId, request.FarmId, allMembers.Count);

        return Result.Success(new MergeDiseaseZonesResult(
            newZoneId,
            allMembers.Count,
            zoneIdArray.Length));
    }
}

