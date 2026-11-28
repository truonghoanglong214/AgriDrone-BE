using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Application.Abstractions.Maps;
using AgriDrone.Modules.Farms.Application.Services;
using AgriDrone.Modules.Farms.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Farms.Application.Features.Maps.GetFarmBaseMapDetail;

internal sealed class GetFarmBaseMapDetailQueryHandler(
    FarmsDbContext dbContext,
    FarmBaseMapReadAccess readAccess,
    IFarmBaseMapPreviewReader previewReader)
    : IRequestHandler<GetFarmBaseMapDetailQuery, Result<FarmBaseMapDetailResponse>>
{
    private sealed record PlantQueryRow(
        Guid Id,
        string PlantCode,
        int? RowIndex,
        int? ColumnIndex,
        double Latitude,
        double Longitude,
        string LifecycleStatus,
        Guid? PlantProfileId);

    private sealed record VersionCandidateLinkRow(
        Guid SourceCandidateId,
        Guid? PlantProfileId,
        Guid? TargetPlantId);

    private sealed record CandidateQueryRow(
        Guid CandidateId,
        int? RowIndex,
        int? ColumnIndex,
        double Latitude,
        double Longitude,
        string ReviewDecision);

    private sealed record MissionInfoRow(
        Guid Id,
        string MissionCode,
        DateTimeOffset? StartedAt,
        DateTimeOffset? ScheduledAt);

    private sealed record ManualChangeEventRow(
        Guid Id,
        Guid? PlantId,
        string ChangeType,
        string Source,
        int? OldRow,
        int? NewRow,
        int? OldCol,
        int? NewCol,
        double? OldLat,
        double? OldLon,
        double? NewLat,
        double? NewLon,
        string? OldLifecycleStatus,
        string? NewLifecycleStatus,
        string? Notes,
        DateTimeOffset CreatedAt);

    public async Task<Result<FarmBaseMapDetailResponse>> Handle(
        GetFarmBaseMapDetailQuery request,
        CancellationToken cancellationToken)
    {
        var version = await dbContext.FarmBaseMapVersions.AsNoTracking()
            .SingleOrDefaultAsync(v => v.Id == request.VersionId && v.FarmId == request.FarmId, cancellationToken);

        if (version is null)
        {
            return Result.Failure<FarmBaseMapDetailResponse>(
                AppError.NotFound("FarmBaseMapVersion.NotFound", $"Base map version {request.VersionId} not found for this farm."));
        }

        var access = await readAccess.CheckAsync(version.TenantId, version.FarmId, cancellationToken);
        if (!access.IsAllowed || (version.Status == FarmBaseMapStatus.Draft && !access.CanReadDraft))
        {
            return Result.Failure<FarmBaseMapDetailResponse>(
                AppError.NotFound("FarmBaseMapVersion.NotFound", "Base map version not found for this farm."));
        }

        if (!access.CanReadDraft && !await previewReader.CanOwnerReadAsync(
                version.TenantId, version.FarmId, version.SourceSurveyOrderId, cancellationToken))
        {
            return Result.Failure<FarmBaseMapDetailResponse>(AppError.Conflict(
                "FarmBaseMapVersion.PaymentRequired", "Reviewed survey results remain locked until final settlement."));
        }

        // 1. Ranh giới Farm Boundary GeoJSON
        string? boundaryGeoJson = null;
        if (version.FarmBoundaryVersionId.HasValue)
        {
            boundaryGeoJson = await dbContext.Database.SqlQueryRaw<string>(
                @"SELECT ST_AsGeoJSON(geometry) AS ""Value""
                  FROM farm.farm_boundaries
                  WHERE id = {0}",
                version.FarmBoundaryVersionId.Value)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (boundaryGeoJson is null)
        {
            boundaryGeoJson = await dbContext.Database.SqlQueryRaw<string>(
                @"SELECT ST_AsGeoJSON(geometry) AS ""Value""
                  FROM farm.farm_boundaries
                  WHERE farm_id = {0} AND status = 'APPROVED'
                  ORDER BY reviewed_at DESC NULLS LAST, created_at DESC
                  LIMIT 1",
                request.FarmId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // 2. Lấy danh sách cây thuộc version này
        var plantItems = new List<BaseMapPlantItemDto>();

        if (version.Status is FarmBaseMapStatus.InternalApproved or FarmBaseMapStatus.Published or FarmBaseMapStatus.Superseded)
        {
            var plantRows = await dbContext.Database.SqlQueryRaw<PlantQueryRow>(
                @"SELECT p.id AS ""Id"", p.plant_code AS ""PlantCode"",
                         p.row_index AS ""RowIndex"", p.column_index AS ""ColumnIndex"",
                         ST_Y(p.location::geometry) AS ""Latitude"",
                         ST_X(p.location::geometry) AS ""Longitude"",
                         p.lifecycle_status::text AS ""LifecycleStatus"",
                         p.plant_profile_id AS ""PlantProfileId""
                  FROM plant.plants p
                  WHERE p.farm_id = {0}
                    AND (
                        p.farm_base_map_version_id = {1}
                        OR EXISTS (
                            SELECT 1
                            FROM plant.plant_profile_observations o
                            WHERE o.farm_id = p.farm_id
                              AND o.plant_profile_id = p.plant_profile_id
                              AND o.farm_base_map_version_id = {1}
                        )
                        OR EXISTS (
                            SELECT 1
                            FROM plant.plant_change_events e
                            WHERE e.farm_id = p.farm_id
                              AND e.plant_id = p.id
                              AND e.farm_base_map_version_id = {1}
                              AND e.source::text = 'MISSION_AI'
                        )
                    )
                  ORDER BY p.row_index, p.column_index",
                request.FarmId, version.Id)
                .ToListAsync(cancellationToken);

            var candidateLinks = await dbContext.Database.SqlQueryRaw<VersionCandidateLinkRow>(
                @"SELECT mc.candidate_id AS ""SourceCandidateId"",
                         i.plant_profile_id AS ""PlantProfileId"",
                         COALESCE(i.target_plant_id, mc.corrected_matched_plant_id, mc.proposed_matched_plant_id) AS ""TargetPlantId""
                  FROM farm.mapping_candidates mc
                  JOIN farm.mapping_candidate_batches b ON b.id = mc.batch_id
                  LEFT JOIN survey.survey_order_pole_count_snapshot_items i
                    ON i.mapping_candidate_id = mc.id
                   AND i.mapping_candidate_batch_id = mc.batch_id
                  WHERE b.farm_id = {0}
                    AND b.farm_base_map_version_id = {1}
                    AND mc.review_decision IN ('CreateNew', 'MatchExisting', 'Relocate', 'Replace')",
                request.FarmId, version.Id)
                .ToListAsync(cancellationToken);

            var candidateByProfile = new Dictionary<Guid, Guid>();
            var candidateByPlant = new Dictionary<Guid, Guid>();
            foreach (var link in candidateLinks)
            {
                if (link.PlantProfileId.HasValue)
                    candidateByProfile.TryAdd(link.PlantProfileId.Value, link.SourceCandidateId);
                if (link.TargetPlantId.HasValue)
                    candidateByPlant.TryAdd(link.TargetPlantId.Value, link.SourceCandidateId);
            }

            // Nếu là version cũ đã bị superseded và plant.plants đã chuyển sang map version mới hơn,
            // fallback lấy từ mapping_candidates của batch thuộc version này
            if (plantRows.Count == 0)
            {
                var candidateRows = await dbContext.Database.SqlQueryRaw<CandidateQueryRow>(
                    @"SELECT c.candidate_id AS ""CandidateId"",
                             COALESCE(c.corrected_row_index, c.proposed_row_index) AS ""RowIndex"",
                             COALESCE(c.corrected_column_index, c.proposed_column_index) AS ""ColumnIndex"",
                             ST_Y(COALESCE(c.corrected_position, c.original_position)::geometry) AS ""Latitude"",
                             ST_X(COALESCE(c.corrected_position, c.original_position)::geometry) AS ""Longitude"",
                             c.review_decision::text AS ""ReviewDecision""
                      FROM farm.mapping_candidates c
                      JOIN farm.mapping_candidate_batches b ON b.id = c.batch_id
                      WHERE b.farm_base_map_version_id = {0}
                        AND c.review_decision IN ('CreateNew', 'MatchExisting', 'Relocate', 'Replace')
                      ORDER BY COALESCE(c.corrected_row_index, c.proposed_row_index), COALESCE(c.corrected_column_index, c.proposed_column_index)",
                    version.Id)
                    .ToListAsync(cancellationToken);

                plantItems = candidateRows.Select(c => new BaseMapPlantItemDto(
                    PlantId: c.CandidateId,
                    PlantCode: $"P-{request.FarmId.ToString()[..4]}-{c.RowIndex ?? 0:D3}-{c.ColumnIndex ?? 0:D3}",
                    Row: c.RowIndex ?? 0,
                    Col: c.ColumnIndex ?? 0,
                    Lat: c.Latitude,
                    Lon: c.Longitude,
                    LifecycleStatus: "HISTORICAL",
                    MappingCandidateId: c.CandidateId)).ToList();
            }
            else
            {
                plantItems = plantRows.Select(p =>
                {
                    Guid? candidateId = p.PlantProfileId is Guid profileId && candidateByProfile.TryGetValue(profileId, out var byProfile)
                        ? byProfile
                        : candidateByPlant.TryGetValue(p.Id, out var byPlant)
                            ? byPlant
                            : null;

                    return new BaseMapPlantItemDto(
                        PlantId: p.Id,
                        PlantCode: p.PlantCode,
                        Row: p.RowIndex ?? 0,
                        Col: p.ColumnIndex ?? 0,
                        Lat: p.Latitude,
                        Lon: p.Longitude,
                        LifecycleStatus: p.LifecycleStatus,
                        PlantProfileId: p.PlantProfileId,
                        MappingCandidateId: candidateId);
                }).ToList();
            }
        }
        else // Draft version: Lấy trực tiếp từ mapping candidate batch
        {
            var candidateRows = await dbContext.Database.SqlQueryRaw<CandidateQueryRow>(
                @"SELECT c.candidate_id AS ""CandidateId"",
                         COALESCE(c.corrected_row_index, c.proposed_row_index) AS ""RowIndex"",
                         COALESCE(c.corrected_column_index, c.proposed_column_index) AS ""ColumnIndex"",
                         ST_Y(COALESCE(c.corrected_position, c.original_position)::geometry) AS ""Latitude"",
                         ST_X(COALESCE(c.corrected_position, c.original_position)::geometry) AS ""Longitude"",
                         c.review_decision::text AS ""ReviewDecision""
                  FROM farm.mapping_candidates c
                  JOIN farm.mapping_candidate_batches b ON b.id = c.batch_id
                  WHERE b.farm_base_map_version_id = {0}
                    AND c.review_decision != 'Reject'
                  ORDER BY COALESCE(c.corrected_row_index, c.proposed_row_index), COALESCE(c.corrected_column_index, c.proposed_column_index)",
                version.Id)
                .ToListAsync(cancellationToken);

            plantItems = candidateRows.Select(c => new BaseMapPlantItemDto(
                PlantId: c.CandidateId,
                PlantCode: $"CANDIDATE-{c.RowIndex ?? 0:D3}-{c.ColumnIndex ?? 0:D3}",
                Row: c.RowIndex ?? 0,
                Col: c.ColumnIndex ?? 0,
                Lat: c.Latitude,
                Lon: c.Longitude,
                LifecycleStatus: "CANDIDATE")).ToList();
        }

        // 3. Tính toán Bounds bao quát
        MapBoundsDto? bounds = null;
        if (plantItems.Count > 0)
        {
            bounds = new MapBoundsDto(
                MinLat: plantItems.Min(p => p.Lat) - 0.0002,
                MaxLat: plantItems.Max(p => p.Lat) + 0.0002,
                MinLon: plantItems.Min(p => p.Lon) - 0.0002,
                MaxLon: plantItems.Max(p => p.Lon) + 0.0002);
        }

        // 4. Lấy thông tin Mission nguồn
        MissionInfoRow? missionInfo = null;
        if (version.SourceMissionId.HasValue)
        {
            missionInfo = await dbContext.Database.SqlQueryRaw<MissionInfoRow>(
                @"SELECT id AS ""Id"", mission_code AS ""MissionCode"", started_at AS ""StartedAt"", scheduled_at AS ""ScheduledAt""
                  FROM mission.drone_missions
                  WHERE id = {0}",
                version.SourceMissionId.Value)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // 5. Lấy các sự kiện biến động thủ công từ bảng phụ plant_change_events
        var manualChangeRows = await dbContext.Database.SqlQueryRaw<ManualChangeEventRow>(
            @"SELECT id AS ""Id"", plant_id AS ""PlantId"",
                     change_type::text AS ""ChangeType"", source::text AS ""Source"",
                     old_row_index AS ""OldRow"", new_row_index AS ""NewRow"",
                     old_column_index AS ""OldCol"", new_column_index AS ""NewCol"",
                     ST_Y(old_location::geometry) AS ""OldLat"", ST_X(old_location::geometry) AS ""OldLon"",
                     ST_Y(new_location::geometry) AS ""NewLat"", ST_X(new_location::geometry) AS ""NewLon"",
                     old_lifecycle_status::text AS ""OldLifecycleStatus"",
                     new_lifecycle_status::text AS ""NewLifecycleStatus"",
                     notes AS ""Notes"", created_at AS ""CreatedAt""
              FROM plant.plant_change_events
              WHERE farm_id = {0} AND farm_base_map_version_id = {1}
                AND source::text = 'MANUAL'
              ORDER BY created_at DESC",
            request.FarmId, version.Id)
            .ToListAsync(cancellationToken);

        var manualChanges = manualChangeRows.Select(m => new BaseMapManualChangeDto(
            EventId: m.Id,
            PlantId: m.PlantId,
            ChangeType: m.ChangeType,
            Source: m.Source,
            OldRow: m.OldRow,
            NewRow: m.NewRow,
            OldCol: m.OldCol,
            NewCol: m.NewCol,
            OldLat: m.OldLat,
            OldLon: m.OldLon,
            NewLat: m.NewLat,
            NewLon: m.NewLon,
            OldLifecycleStatus: m.OldLifecycleStatus,
            NewLifecycleStatus: m.NewLifecycleStatus,
            Notes: m.Notes,
            CreatedAt: m.CreatedAt)).ToList();

        var preview = version.SourceMissionId.HasValue
            ? await previewReader.GetAsync(version.TenantId, version.FarmId, version.SourceMissionId.Value, cancellationToken)
            : null;

        var activePoles = plantItems.Count(p => p.LifecycleStatus != "REMOVED");

        var response = new FarmBaseMapDetailResponse(
            VersionId: version.Id,
            FarmId: version.FarmId,
            VersionNumber: version.VersionNumber,
            Status: version.Status.ToString().ToUpperInvariant(),
            SourceMissionId: version.SourceMissionId,
            SourceMissionCode: missionInfo?.MissionCode,
            FlightDate: missionInfo?.StartedAt ?? missionInfo?.ScheduledAt ?? version.CreatedAt,
            SourceSurveyOrderId: version.SourceSurveyOrderId,
            FarmBoundaryVersionId: version.FarmBoundaryVersionId,
            BoundaryGeoJson: boundaryGeoJson,
            PreviewUrl: preview?.Url,
            Bounds: preview?.Bounds ?? bounds,
            GridBearingDeg: version.GridBearingDeg,
            RowSpacingM: version.RowSpacingM,
            PlantSpacingM: version.PlantSpacingM,
            TotalPoles: activePoles > 0 ? activePoles : plantItems.Count,
            PublishedAt: version.PublishedAt,
            PublishedBy: version.PublishedBy,
            CreatedAt: version.CreatedAt,
            Plants: plantItems,
            ManualChanges: manualChanges);

        return Result.Success(response);
    }
}

