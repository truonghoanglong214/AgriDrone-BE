using System.Text.Json;
using AgriDrone.Modules.Missions.Domain.Observations;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgriDrone.Modules.Missions.Application.Features.PlantHealth;

/// <summary>
/// UC-29: Xu ly diem bi danh co PENDING (cay lech toa do > 2.5m hoac ngoai ranh gioi).
///
/// 3 lua chon:
/// - SNAP              -> gan resolved_plant_id & suggested_plant_id sang TargetPlantId da chon, doi status sang MATCHED
/// - CREATE_NEW_PLANT  -> Kiem tra ST_Covers voi ranh gioi trang trai (chan neu nam ngoai), INSERT vao plant.plants vi tri phat hien, doi status sang NEW_PLANT
/// - DISMISS           -> Bac bo False Positive, doi status sang REJECTED
///
/// SAU KHI XU LY: ban do Leaflet load lai dung (xoa co Pending, hien dung cay moi/mau cay).
/// </summary>
internal sealed class ResolveFlaggedObservationCommandHandler(
    MissionsDbContext dbContext,
    ISystemManagerAccessService managerAccessService,
    ILogger<ResolveFlaggedObservationCommandHandler> logger)
    : IRequestHandler<ResolveFlaggedObservationCommand, Result<ResolveFlaggedObservationResult>>
{
    private sealed record ObservationRecord(
        Guid Id,
        Guid MissionId,
        Guid FarmId,
        Guid? SuggestedPlantId,
        Guid? ResolvedPlantId,
        string ReviewStatus,
        double? DetectedLat,
        double? DetectedLon);

    public async Task<Result<ResolveFlaggedObservationResult>> Handle(
        ResolveFlaggedObservationCommand request,
        CancellationToken cancellationToken)
    {
        var access = await managerAccessService.ResolveFarmAccessAsync(
            request.FarmId, cancellationToken);
        if (!access.IsAllowed)
        {
            return Result.Failure<ResolveFlaggedObservationResult>(
                AppError.Forbidden("ResolveFlagged.FarmAccessDenied", "Access denied."));
        }

        var action = request.Action.Trim().ToUpperInvariant();
        if (action is not ("SNAP" or "CREATE_NEW_PLANT" or "DISMISS"))
        {
            return Result.Failure<ResolveFlaggedObservationResult>(
                AppError.Validation("ResolveFlagged.InvalidAction",
                    "Action must be 'SNAP', 'CREATE_NEW_PLANT', or 'DISMISS'."));
        }

        // Lay observation
        var obs = await dbContext.Database.SqlQueryRaw<ObservationRecord>(@"
            SELECT id AS ""Id"", mission_id AS ""MissionId"", farm_id AS ""FarmId"",
                   suggested_plant_id AS ""SuggestedPlantId"",
                   resolved_plant_id AS ""ResolvedPlantId"",
                   review_status::text AS ""ReviewStatus"",
                   ST_Y(detected_location::geometry) AS ""DetectedLat"",
                   ST_X(detected_location::geometry) AS ""DetectedLon""
            FROM mission.mission_plant_observations
            WHERE id = {0} AND farm_id = {1}",
            request.ObservationId, request.FarmId)
            .FirstOrDefaultAsync(cancellationToken);

        if (obs is null)
        {
            return Result.Failure<ResolveFlaggedObservationResult>(
                AppError.NotFound("ResolveFlagged.NotFound",
                    $"Observation {request.ObservationId} not found."));
        }

        if (await PlantHealthLegacyWriteGuard.IsV4Async(dbContext, request.ObservationId, request.FarmId, cancellationToken))
            return Result.Failure<ResolveFlaggedObservationResult>(AppError.Conflict("PlantHealth.V4ReviewRequired",
                "Resolve this observation through its pending Plant Health result."));

        if (obs.ReviewStatus?.ToUpperInvariant() is not "PENDING")
        {
            return Result.Failure<ResolveFlaggedObservationResult>(
                AppError.Conflict("ResolveFlagged.NotPending",
                    $"Observation status is '{obs.ReviewStatus}', only PENDING can be resolved."));
        }

        var now = DateTimeOffset.UtcNow;
        Guid? resolvedPlantId = null;
        Guid? newPlantId = null;
        string? newPlantCode = null;
        string newStatus;

        switch (action)
        {
            case "SNAP":
            {
                // Validate target plant ton tai trong Farm
                if (!request.TargetPlantId.HasValue)
                {
                    return Result.Failure<ResolveFlaggedObservationResult>(
                        AppError.Validation("ResolveFlagged.TargetPlantRequired",
                            "TargetPlantId is required for SNAP action."));
                }

                var targetExists = await dbContext.Database.SqlQueryRaw<int>(@"
                    SELECT COUNT(*)::int AS ""Value""
                    FROM plant.plants
                    WHERE id = {0} AND farm_id = {1}
                      AND lifecycle_status = 'ACTIVE'::system.plant_lifecycle_status",
                    request.TargetPlantId.Value, request.FarmId)
                    .FirstOrDefaultAsync(cancellationToken);

                if (targetExists == 0)
                {
                    return Result.Failure<ResolveFlaggedObservationResult>(
                        AppError.NotFound("ResolveFlagged.TargetPlantNotFound",
                            $"Target plant {request.TargetPlantId} not found in farm."));
                }

                resolvedPlantId = request.TargetPlantId.Value;
                newStatus = "MATCHED";

                // Cap nhat observation: gan ca resolved_plant_id va suggested_plant_id de GetFarmPlantHealthMap hien thi dung
                await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
                    UPDATE mission.mission_plant_observations
                    SET resolved_plant_id = {resolvedPlantId},
                        suggested_plant_id = {resolvedPlantId},
                        review_status = 'MATCHED'::system.observation_review_status,
                        reviewed_at = {now}
                    WHERE id = {request.ObservationId}",
                    cancellationToken);

                // Cap nhat health level cho cay dich sang MODERATE (co benh)
                await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
                    UPDATE plant.plants
                    SET current_health_level_id = (
                        SELECT id FROM plant.health_levels WHERE code = 'MODERATE' LIMIT 1
                    ), last_inspected_at = {now}, updated_at = {now}
                    WHERE id = {resolvedPlantId.Value} AND farm_id = {request.FarmId}",
                    cancellationToken);

                // Dong bo scan sang MODERATE neu co
                await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
                    UPDATE plant.plant_scans
                    SET overall_health_level_id = (
                        SELECT id FROM plant.health_levels WHERE code = 'MODERATE' LIMIT 1
                    )
                    WHERE mission_id = {obs.MissionId} AND plant_id = {resolvedPlantId.Value}",
                    cancellationToken);

                break;
            }

            case "CREATE_NEW_PLANT":
            {
                var newPlantCodeCandidate = string.IsNullOrWhiteSpace(request.NewPlantCode)
                    ? $"P-NEW-{DateTime.UtcNow:MMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}"
                    : request.NewPlantCode.Trim().ToUpperInvariant();

                if (!obs.DetectedLat.HasValue || !obs.DetectedLon.HasValue)
                {
                    return Result.Failure<ResolveFlaggedObservationResult>(
                        AppError.Validation("ResolveFlagged.NoDetectedLocation",
                            "Observation has no detected GPS location to place new plant."));
                }

                // Kiem tra vi tri cay co nam trong ranh gioi trang trai (farm boundary) bang PostGIS ST_Covers
                var boundaryRows = await dbContext.Database.SqlQueryRaw<bool>(@"
                    SELECT ST_Covers(fb.geometry, ST_SetSRID(ST_MakePoint({0}, {1}), 4326)) AS ""Value""
                    FROM farm.farm_boundaries fb
                    WHERE fb.farm_id = {2}
                      AND fb.status = 'APPROVED'::system.farm_boundary_status
                    ORDER BY fb.version_number DESC
                    LIMIT 1",
                    obs.DetectedLon.Value, obs.DetectedLat.Value, request.FarmId)
                    .ToListAsync(cancellationToken);

                if (boundaryRows.Count > 0 && !boundaryRows[0])
                {
                    return Result.Failure<ResolveFlaggedObservationResult>(
                        AppError.Validation("ResolveFlagged.OutsideBoundary",
                            "Vị trí cây nằm ngoài ranh giới trang trại. Vui lòng chọn DISMISS hoặc cập nhật ranh giới vườn."));
                }

                // Kiem tra code chua ton tai trong farm
                var codeExists = await dbContext.Database.SqlQueryRaw<int>(@"
                    SELECT COUNT(*)::int AS ""Value""
                    FROM plant.plants
                    WHERE farm_id = {0} AND plant_code = {1}
                      AND lifecycle_status = 'ACTIVE'::system.plant_lifecycle_status",
                    request.FarmId, newPlantCodeCandidate)
                    .FirstOrDefaultAsync(cancellationToken);

                if (codeExists > 0)
                {
                    return Result.Failure<ResolveFlaggedObservationResult>(
                        AppError.Conflict("ResolveFlagged.PlantCodeExists",
                            $"Plant code '{newPlantCodeCandidate}' already exists in this farm."));
                }

                newPlantId = Guid.NewGuid();
                newPlantCode = newPlantCodeCandidate;
                resolvedPlantId = newPlantId;
                newStatus = "NEW_PLANT";

                // Lay health level MODERATE
                var moderateLevelId = await dbContext.Database.SqlQueryRaw<Guid>(@"
                    SELECT id AS ""Value"" FROM plant.health_levels
                    WHERE code = 'MODERATE' LIMIT 1")
                    .FirstOrDefaultAsync(cancellationToken);

                // INSERT cay moi vao plant.plants tai vi tri phat hien
                // row_index & column_index la null (unmapped pole khong nam tren grid ban dau)
                await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
                    INSERT INTO plant.plants (
                        id, farm_id, zone_id, plant_code,
                        location, position_source, lifecycle_status,
                        current_health_level_id, last_inspected_at,
                        created_at, updated_at
                    ) VALUES (
                        {newPlantId.Value},
                        {request.FarmId},
                        {request.ZoneId},
                        {newPlantCode},
                        ST_SetSRID(ST_MakePoint({obs.DetectedLon.Value}, {obs.DetectedLat.Value}), 4326),
                        'MANUAL'::system.position_source,
                        'ACTIVE'::system.plant_lifecycle_status,
                        {moderateLevelId},
                        {now},
                        {now},
                        {now}
                    )",
                    cancellationToken);

                // Ghi event thay doi vao audit trail
                await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
                    INSERT INTO plant.plant_change_events (
                        id, farm_id, plant_id, event_type, notes, created_at
                    ) VALUES (
                        {Guid.NewGuid()},
                        {request.FarmId},
                        {newPlantId.Value},
                        'MANUAL_REGISTERED'::system.plant_change_event_type,
                        {$"[Disease Detection] New plant created from flagged observation {request.ObservationId}. Reason: {request.Reason}"},
                        {now}
                    )",
                    cancellationToken);

                // Cap nhat observation: gan ca resolved_plant_id va suggested_plant_id
                await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
                    UPDATE mission.mission_plant_observations
                    SET resolved_plant_id = {newPlantId.Value},
                        suggested_plant_id = {newPlantId.Value},
                        review_status = 'NEW_PLANT'::system.observation_review_status,
                        reviewed_at = {now}
                    WHERE id = {request.ObservationId}",
                    cancellationToken);

                // Them plant_scan cho cay moi
                var newScanId = Guid.NewGuid();
                await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
                    INSERT INTO plant.plant_scans (
                        id, farm_id, plant_id, mission_id,
                        observed_at, source, overall_health_level_id,
                        created_at, server_received_at
                    ) VALUES (
                        {newScanId},
                        {request.FarmId},
                        {newPlantId.Value},
                        {obs.MissionId},
                        {now},
                        'DRONE_AI'::system.scan_source,
                        {moderateLevelId},
                        {now}, {now}
                    )",
                    cancellationToken);

                logger.LogInformation(
                    "New plant {PlantId} ({Code}) created from flagged observation {ObsId}",
                    newPlantId, newPlantCode, request.ObservationId);

                break;
            }

            case "DISMISS":
            default:
            {
                newStatus = "REJECTED";

                await dbContext.Database.ExecuteSqlInterpolatedAsync($@"
                    UPDATE mission.mission_plant_observations
                    SET review_status = 'REJECTED'::system.observation_review_status,
                        reviewed_at = {now}
                    WHERE id = {request.ObservationId}",
                    cancellationToken);

                logger.LogInformation(
                    "Flagged observation {ObsId} dismissed (False Positive / Noise)",
                    request.ObservationId);

                break;
            }
        }

        return Result.Success(new ResolveFlaggedObservationResult(
            request.ObservationId,
            action,
            newStatus,
            resolvedPlantId,
            newPlantId,
            newPlantCode));
    }
}

