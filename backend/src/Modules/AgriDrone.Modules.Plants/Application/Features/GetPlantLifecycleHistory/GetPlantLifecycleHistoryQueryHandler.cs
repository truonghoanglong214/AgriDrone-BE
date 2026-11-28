using System.Data;
using AgriDrone.Modules.Plants.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Plants.Application.Features.GetPlantLifecycleHistory;

internal sealed class GetPlantLifecycleHistoryQueryHandler(
    PlantsDbContext dbContext,
    IExecutionContext executionContext,
    IEffectiveAccessService effectiveAccess,
    ISystemManagerAccessService managerAccess)
    : IRequestHandler<GetPlantLifecycleHistoryQuery, Result<PlantLifecycleHistoryResponse>>
{
    private sealed record PlantRow(
        Guid Id,
        Guid FarmId,
        string PlantCode,
        int? RowIndex,
        int? ColumnIndex,
        double? Lat,
        double? Lon,
        string LifecycleStatus,
        Guid? CurrentMapVersionId,
        DateTimeOffset? MappedAt,
        DateTimeOffset CreatedAt);

    private sealed record ChangeEventRow(
        Guid Id,
        string ChangeType,
        string Source,
        double? OldLat,
        double? OldLon,
        double? NewLat,
        double? NewLon,
        int? OldRow,
        int? NewRow,
        int? OldCol,
        int? NewCol,
        string? Notes,
        Guid? MissionId,
        string? MissionCode,
        Guid? CreatedBy,
        DateTimeOffset CreatedAt,
        Guid? FarmBaseMapVersionId);

    public async Task<Result<PlantLifecycleHistoryResponse>> Handle(
        GetPlantLifecycleHistoryQuery request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is not Guid actorId)
            return PlantNotFound();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead, cancellationToken);

        var farmTenant = await dbContext.Database.SqlQuery<Guid?>($"""
            SELECT tenant_id AS "Value"
            FROM farm.farms
            WHERE id = {request.FarmId} AND status = 'ACTIVE' AND deleted_at IS NULL
            """).SingleOrDefaultAsync(cancellationToken);
        if (farmTenant is not Guid tenantId)
            return PlantNotFound();

        var manager = await managerAccess.ResolveFarmAccessAsync(request.FarmId, cancellationToken);
        var canReadInternal = manager.IsAllowed && manager.TenantId == tenantId && manager.FarmId == request.FarmId;
        if (!canReadInternal &&
            (executionContext.TenantId != tenantId ||
             !(await effectiveAccess.CheckFarmAsync(
                 actorId, tenantId, request.FarmId, FarmAccessLevel.Member, cancellationToken)).IsAllowed))
        {
            return PlantNotFound();
        }

        // Internal approval can mutate the live plant before release. Require release
        // proof for every V3 change affecting the live row or its timeline.
        var plant = await dbContext.Database.SqlQuery<PlantRow>($"""
            WITH released_v3_maps AS (
                SELECT m.id AS map_id, o.id AS order_id, m.source_mission_id, r.published_at
                FROM survey.v3_survey_releases r
                JOIN farm.farm_base_map_versions m ON m.id = r.map_id
                    AND m.source_survey_order_id = r.survey_order_id
                    AND m.tenant_id = r.tenant_id AND m.farm_id = r.farm_id
                JOIN survey.survey_orders o ON o.id = r.survey_order_id
                    AND o.tenant_id = r.tenant_id AND o.farm_id = r.farm_id
                WHERE NOT {canReadInternal}
                  AND r.tenant_id = {tenantId} AND r.farm_id = {request.FarmId}
                  AND o.scope = 'V3'::system.contract_scope AND o.status <> 'CANCELLED'
                  AND m.status IN ('PUBLISHED', 'SUPERSEDED')
                  AND NOT EXISTS (
                      SELECT 1
                      FROM survey.quality_refund_cases c
                      WHERE c.v3_publication_id = r.id AND c.status = 'Refunded'
                  )
            )
            SELECT p.id AS "Id", p.farm_id AS "FarmId", p.plant_code AS "PlantCode",
                   p.row_index AS "RowIndex", p.column_index AS "ColumnIndex",
                   ST_Y(p.location::geometry) AS "Lat", ST_X(p.location::geometry) AS "Lon",
                   p.lifecycle_status::text AS "LifecycleStatus",
                   p.farm_base_map_version_id AS "CurrentMapVersionId",
                   p.mapped_at AS "MappedAt", p.created_at AS "CreatedAt"
            FROM plant.plants p
            WHERE p.id = {request.PlantId} AND p.farm_id = {request.FarmId}
              AND (
                  {canReadInternal}
                  OR (
                      (
                          p.farm_base_map_version_id IS NULL
                          OR EXISTS (
                              SELECT 1
                              FROM farm.farm_base_map_versions m
                              JOIN survey.survey_orders o ON o.id = m.source_survey_order_id
                                  AND o.tenant_id = m.tenant_id AND o.farm_id = m.farm_id
                              WHERE m.id = p.farm_base_map_version_id
                                AND m.tenant_id = {tenantId} AND m.farm_id = p.farm_id
                                AND m.status IN ('PUBLISHED', 'SUPERSEDED')
                                AND o.scope = 'LEGACY'::system.contract_scope
                                AND o.status <> 'CANCELLED'
                          )
                           OR EXISTS (
                               SELECT 1
                               FROM released_v3_maps r
                               WHERE r.map_id = p.farm_base_map_version_id
                           )
                      )
                      AND NOT EXISTS (
                          SELECT 1
                          FROM mission.drone_missions dm
                          LEFT JOIN survey.survey_orders o ON o.id = dm.survey_order_id
                          WHERE dm.id = p.created_from_mission_id
                            AND (dm.capture_attempt_id IS NOT NULL OR o.scope = 'V3'::system.contract_scope)
                             AND NOT EXISTS (
                                 SELECT 1
                                 FROM released_v3_maps r
                                 WHERE r.order_id = dm.survey_order_id AND r.source_mission_id = dm.id
                             )
                      )
                      AND NOT EXISTS (
                          SELECT 1
                          FROM plant.plant_change_events e
                          LEFT JOIN farm.farm_base_map_versions em ON em.id = e.farm_base_map_version_id
                          LEFT JOIN mission.drone_missions dm ON dm.id = e.mission_id
                          WHERE e.farm_id = p.farm_id
                            AND (
                                e.plant_id = p.id
                                OR e.plant_id IN (
                                    SELECT replacement.id
                                    FROM plant.plants replacement
                                    WHERE replacement.replaced_plant_id = p.id AND replacement.farm_id = p.farm_id
                                )
                            )
                            AND (
                                dm.capture_attempt_id IS NOT NULL
                                OR EXISTS (
                                    SELECT 1
                                    FROM survey.survey_orders o
                                    WHERE o.id IN (e.source_survey_order_id, em.source_survey_order_id, dm.survey_order_id)
                                      AND o.scope = 'V3'::system.contract_scope
                                )
                            )
                             AND NOT EXISTS (
                                 SELECT 1
                                 FROM released_v3_maps r
                                 WHERE r.map_id = e.farm_base_map_version_id
                                   AND (e.source_survey_order_id IS NULL OR e.source_survey_order_id = r.order_id)
                                   AND (e.mission_id IS NULL OR e.mission_id = r.source_mission_id)
                                   AND e.created_at <= r.published_at
                             )
                      )
                  )
              )
            """).SingleOrDefaultAsync(cancellationToken);

        if (plant is null)
            return PlantNotFound();

        // 2. Lấy toàn bộ sự kiện biến động (events) của cây này (bao gồm cả biến động từ bảng phụ farm_base_map_version_id)
        var events = await dbContext.Database.SqlQueryRaw<ChangeEventRow>(
            @"SELECT e.id AS ""Id"", e.change_type::text AS ""ChangeType"", e.source::text AS ""Source"",
                     ST_Y(e.old_location::geometry) AS ""OldLat"", ST_X(e.old_location::geometry) AS ""OldLon"",
                     ST_Y(e.new_location::geometry) AS ""NewLat"", ST_X(e.new_location::geometry) AS ""NewLon"",
                     e.old_row_index AS ""OldRow"", e.new_row_index AS ""NewRow"",
                     e.old_column_index AS ""OldCol"", e.new_column_index AS ""NewCol"",
                     e.notes AS ""Notes"", e.mission_id AS ""MissionId"",
                     m.mission_code AS ""MissionCode"",
                     e.created_by AS ""CreatedBy"", e.created_at AS ""CreatedAt"",
                     e.farm_base_map_version_id AS ""FarmBaseMapVersionId""
              FROM plant.plant_change_events e
              LEFT JOIN mission.drone_missions m ON m.id = e.mission_id
              WHERE e.plant_id = {0} AND e.farm_id = {1}
              ORDER BY e.created_at ASC",
            request.PlantId, request.FarmId)
            .ToListAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        var timeline = new List<PlantLifecycleEventItemDto>();

        foreach (var e in events)
        {
            GeoPointDto? oldPos = (e.OldLat.HasValue && e.OldLon.HasValue) ? new GeoPointDto(e.OldLat.Value, e.OldLon.Value) : null;
            GeoPointDto? newPos = (e.NewLat.HasValue && e.NewLon.HasValue) ? new GeoPointDto(e.NewLat.Value, e.NewLon.Value) : null;

            var summary = GenerateHumanReadableSummary(e);

            timeline.Add(new PlantLifecycleEventItemDto(
                EventId: e.Id,
                ChangeType: e.ChangeType,
                Source: e.Source,
                Summary: summary,
                OldPosition: oldPos,
                NewPosition: newPos,
                OldRow: e.OldRow,
                NewRow: e.NewRow,
                OldCol: e.OldCol,
                NewCol: e.NewCol,
                Notes: e.Notes,
                MissionId: e.MissionId,
                MissionCode: e.MissionCode,
                CreatedBy: e.CreatedBy,
                CreatedAt: e.CreatedAt,
                FarmBaseMapVersionId: e.FarmBaseMapVersionId));
        }

        // Nếu chưa có event nào trong bảng plant_change_events, tạo 1 event ban đầu từ ngày trồng/khởi tạo
        if (timeline.Count == 0)
        {
            timeline.Add(new PlantLifecycleEventItemDto(
                EventId: Guid.NewGuid(),
                ChangeType: "INITIAL_REGISTRATION",
                Source: "SYSTEM",
                Summary: $"Trụ cây được định danh lần đầu tại Hàng {plant.RowIndex ?? 0}, Cột {plant.ColumnIndex ?? 0}",
                OldPosition: null,
                NewPosition: (plant.Lat.HasValue && plant.Lon.HasValue) ? new GeoPointDto(plant.Lat.Value, plant.Lon.Value) : null,
                OldRow: null,
                NewRow: plant.RowIndex,
                OldCol: null,
                NewCol: plant.ColumnIndex,
                Notes: "Khởi tạo từ bản đồ cơ sở",
                MissionId: null,
                MissionCode: null,
                CreatedBy: null,
                CreatedAt: plant.MappedAt ?? plant.CreatedAt,
                FarmBaseMapVersionId: plant.CurrentMapVersionId));
        }

        var response = new PlantLifecycleHistoryResponse(
            PlantId: plant.Id,
            FarmId: plant.FarmId,
            PlantCode: plant.PlantCode,
            CurrentLocation: (plant.Lat.HasValue && plant.Lon.HasValue) ? new GeoPointDto(plant.Lat.Value, plant.Lon.Value) : null,
            CurrentRow: plant.RowIndex,
            CurrentCol: plant.ColumnIndex,
            LifecycleStatus: plant.LifecycleStatus,
            CurrentMapVersionId: plant.CurrentMapVersionId,
            MappedAt: plant.MappedAt,
            CreatedAt: plant.CreatedAt,
            Timeline: timeline);

        return Result.Success(response);
    }

    private static Result<PlantLifecycleHistoryResponse> PlantNotFound() =>
        Result.Failure<PlantLifecycleHistoryResponse>(
            AppError.NotFound("Plant.NotFound", "Plant was not found."));

    private static string GenerateHumanReadableSummary(ChangeEventRow e)
    {
        var type = e.ChangeType?.ToUpperInvariant() ?? string.Empty;
        return type switch
        {
            "NEW_PLANT" or "NEWPLANT" => $"Trụ cây mới được thêm vào bản đồ tại Hàng {e.NewRow ?? 0}, Cột {e.NewCol ?? 0}",
            "MAPPING_DIFFERENCE" or "MAPPINGDIFFERENCE" when e.OldLat.HasValue && e.NewLat.HasValue =>
                $"Nắn chỉnh tọa độ trụ từ (Lat: {e.OldLat:F6}, Lon: {e.OldLon:F6}) sang (Lat: {e.NewLat:F6}, Lon: {e.NewLon:F6})",
            "REMOVED_PLANT" or "REMOVEDPLANT" => "Trụ cây đã bị nhổ bỏ hoặc phá hủy khỏi quy hoạch vườn",
            "DEAD_PLANT" or "DEADPLANT" => "Trụ cây được ghi nhận đã chết",
            "MISSING_PLANT" or "MISSINGPLANT" => "Trụ cây không còn xuất hiện trong đợt quét khảo sát mới",
            _ => !string.IsNullOrWhiteSpace(e.Notes) ? e.Notes : $"Cập nhật trạng thái cây: {e.ChangeType}"
        };
    }
}

