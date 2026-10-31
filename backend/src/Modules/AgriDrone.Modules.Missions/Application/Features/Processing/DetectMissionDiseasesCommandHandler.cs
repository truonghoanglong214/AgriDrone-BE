using System.IO;
using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Application.Abstractions.Processing;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Domain.Observations;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Application.Abstractions.Authorization;
using AgriDrone.SharedKernel.Application.Abstractions.Execution;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Missions.Application.Features.Processing;

internal sealed class DetectMissionDiseasesCommandHandler(
    IDroneMissionRepository missionRepository,
    MissionsDbContext dbContext,
    IMetashapeService metashapeService,
    ISystemManagerAccessService managerAccessService,
    IExecutionContext executionContext,
    ILogger<DetectMissionDiseasesCommandHandler> logger)
    : IRequestHandler<DetectMissionDiseasesCommand, Result<DetectMissionDiseasesResult>>
{
    private sealed record PlantMatchTarget(
        Guid Id,
        string PlantCode,
        double Latitude,
        double Longitude,
        int? RowIndex,
        int? ColumnIndex);

    private sealed record HealthLevelRow(Guid Id, string Code);
    private sealed record PlantConditionRow(Guid Id, string Code, string Name);
    private sealed record BaseMapRefRow(Guid? BaseMapVersionId, Guid? BoundaryVersionId);
    private sealed record SurveyOrderRefRow(Guid? SurveyOrderId, Guid? TenantId);
    private sealed record GuidRow(Guid Value);

    public async Task<Result<DetectMissionDiseasesResult>> Handle(
        DetectMissionDiseasesCommand request,
        CancellationToken cancellationToken)
    {
        if (executionContext.ActorId is null)
        {
            return Result.Failure<DetectMissionDiseasesResult>(MissionError.CurrentUserRequired());
        }

        var access = await managerAccessService.ResolveFarmAccessAsync(
            request.FarmId, cancellationToken);
        if (!access.IsAllowed || access.TenantId != request.TenantId || access.FarmId != request.FarmId)
        {
            return Result.Failure<DetectMissionDiseasesResult>(AppError.Forbidden(
                "DetectDiseases.FarmAccessDenied", "The manager is not assigned to this Farm."));
        }

        var mission = await missionRepository.GetByIdAsync(
            request.MissionId,
            request.TenantId,
            request.FarmId,
            cancellationToken);

        if (mission is null)
        {
            return Result.Failure<DetectMissionDiseasesResult>(
                MissionError.NotFound(request.MissionId));
        }

        // Truy vấn danh sách ảnh thô đã gắn GPS của Mission
        var mediaList = await dbContext.MissionMedia
            .Include(mm => mm.Media)
            .AsNoTracking()
            .Where(mm =>
                mm.MissionId == request.MissionId &&
                mm.MediaRole == MissionMediaRole.RawImage)
            .OrderBy(mm => mm.CapturedAt)
            .ThenBy(mm => mm.Media.StorageKey)
            .ToListAsync(cancellationToken);

        List<MetashapeTaskImageItem>? imageItems = null;
        if (mediaList.Count > 0)
        {
            imageItems = mediaList.Select(mm => new MetashapeTaskImageItem(
                !string.IsNullOrWhiteSpace(mm.Media.StorageKey)
                    ? mm.Media.StorageKey
                    : $"tenants/{request.TenantId}/media/{request.MissionId}/{mm.MediaId:N}.jpg",
                mm.Media.Url)).ToList();
        }

        logger.LogInformation(
            "Executing disease detection for Mission {MissionId} with {Count} images...",
            request.MissionId,
            imageItems?.Count ?? 0);

        var result = await metashapeService.DetectMissionDiseasesAsync(
            request.MissionId,
            request.TenantId,
            imageItems,
            request.ConfidenceThreshold,
            request.PolygonGeoJson,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return Result.Failure<DetectMissionDiseasesResult>(
                AppError.Failure("DetectDiseases.Failed", result.ErrorMessage ?? "Failed to detect diseases."));
        }

        // Lưu kết quả nhận diện bệnh vào cơ sở dữ liệu PostgreSQL
        await SaveDiseaseDetectionsToDatabaseAsync(request, result, mediaList, cancellationToken);

        return Result.Success(result);
    }

    private async Task SaveDiseaseDetectionsToDatabaseAsync(
        DetectMissionDiseasesCommand request,
        DetectMissionDiseasesResult result,
        List<MissionMedia> mediaList,
        CancellationToken cancellationToken)
    {
        if (result.Detections.Count == 0)
        {
            return;
        }

        try
        {
            // 1. Tạo bản đồ tra cứu ảnh gốc để lấy evidence_media_id
            var mediaMap = mediaList
                .Where(mm => !string.IsNullOrEmpty(mm.Media?.StorageKey))
                .GroupBy(mm => Path.GetFileName(mm.Media.StorageKey), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().MediaId, StringComparer.OrdinalIgnoreCase);

            // 2. Lấy danh sách cây hiện có của Farm trong plant.plants để so khớp
            var farmPlants = await dbContext.Database.SqlQueryRaw<PlantMatchTarget>(
                @"SELECT id AS ""Id"", plant_code AS ""PlantCode"", 
                         ST_Y(location::geometry) AS ""Latitude"", 
                         ST_X(location::geometry) AS ""Longitude"",
                         row_index AS ""RowIndex"", column_index AS ""ColumnIndex""
                  FROM plant.plants 
                  WHERE farm_id = {0} AND lifecycle_status = 'ACTIVE'",
                request.FarmId)
                .ToListAsync(cancellationToken);

            // 3. Xóa các quan sát bệnh cũ của mission này để tránh trùng lặp
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM mission.mission_plant_observations WHERE mission_id = {request.MissionId} AND farm_id = {request.FarmId}",
                cancellationToken);

            var observationsToInsert = new List<MissionPlantObservation>();
            var infectedPlantIds = new HashSet<Guid>();

            // Dung sai so khớp GPS (mặc định 2.5m nếu không truyền từ body request)
            var matchingToleranceM = request.MatchingToleranceMeters is > 0
                ? request.MatchingToleranceMeters.Value
                : 2.5;

            foreach (var detection in result.Detections)
            {
                // Luu tat ca quan sat: gom ca qua khoe (Healthy Fruit Green/Yellow/Red) va vet benh (Disease)
                var isDisease = detection.ClassName.StartsWith("Disease", StringComparison.OrdinalIgnoreCase) ||
                                (!detection.ClassName.StartsWith("Healthy", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(detection.ClassName));

                // Tìm cây gần nhất trong vườn
                PlantMatchTarget? nearestPlant = null;
                double minDistance = double.MaxValue;

                foreach (var plant in farmPlants)
                {
                    var dist = CalculateHaversineMeters(detection.Lat, detection.Lon, plant.Latitude, plant.Longitude);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        nearestPlant = plant;
                    }
                }

                Guid? matchedPlantId = null;
                decimal? matchConf = null;
                if (nearestPlant != null && minDistance <= matchingToleranceM)
                {
                    matchedPlantId = nearestPlant.Id;
                    if (isDisease)
                    {
                        infectedPlantIds.Add(nearestPlant.Id);
                    }
                    matchConf = (decimal)Math.Round(Math.Max(0.1, 1.0 - (minDistance / matchingToleranceM)), 4);
                }

                // Tra cứu ảnh media gốc
                Guid? evidenceMediaId = null;
                if (!string.IsNullOrEmpty(detection.ImageName) && mediaMap.TryGetValue(detection.ImageName, out var foundMediaId))
                {
                    evidenceMediaId = foundMediaId;
                }

                // Tạo metadata JSON cho vết bệnh
                var metadataDoc = JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    detection_id = detection.Id,
                    class_id = detection.ClassId,
                    class_name = detection.ClassName,
                    image_name = detection.ImageName,
                    crop_image_url = detection.CropImageUrl,
                    annotated_image_url = detection.AnnotatedImageUrl,
                    bbox = new[] { detection.BboxX, detection.BboxY, detection.BboxW, detection.BboxH },
                    plant_code = nearestPlant?.PlantCode,
                    row = nearestPlant?.RowIndex,
                    col = nearestPlant?.ColumnIndex
                }));

                var observation = MissionPlantObservation.Create(
                    farmId: request.FarmId,
                    missionId: request.MissionId,
                    detectedLocation: new Point(detection.Lon, detection.Lat) { SRID = 4326 },
                    detectionConfidence: (decimal)Math.Clamp(detection.Confidence, 0.0, 1.0),
                    suggestedPlantId: matchedPlantId,
                    resolvedPlantId: matchedPlantId,
                    gpsDistanceM: (decimal)Math.Round(minDistance, 3),
                    matchingParameters: metadataDoc,
                    evidenceMediaId: evidenceMediaId,
                    reviewStatus: matchedPlantId.HasValue ? ObservationReviewStatus.Matched : ObservationReviewStatus.Pending,
                    matchConfidence: matchConf,
                    selectedMatchStrategy: MatchStrategy.GpsOnly,
                    matchingAlgorithmVersion: "best_disease_v1");

                observationsToInsert.Add(observation);
            }

            if (observationsToInsert.Count > 0)
            {
                await dbContext.MissionPlantObservations.AddRangeAsync(observationsToInsert, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogInformation(
                    "Successfully saved {Count} disease observations to database for Mission {MissionId} ({Infected} plants infected).",
                    observationsToInsert.Count, request.MissionId, infectedPlantIds.Count);
            }

            // 4. Cập nhật trạng thái sức khỏe của cây trong bảng plant.plants
            if (farmPlants.Count > 0)
            {
                var healthLevels = await dbContext.Database.SqlQueryRaw<HealthLevelRow>(
                    @"SELECT id AS ""Id"", code AS ""Code"" FROM plant.health_levels WHERE code IN ('HEALTHY', 'MODERATE')")
                    .ToListAsync(cancellationToken);

                var moderateId = healthLevels.FirstOrDefault(h => h.Code == "MODERATE")?.Id;
                var healthyId = healthLevels.FirstOrDefault(h => h.Code == "HEALTHY")?.Id;

                // Cập nhật cây bị bệnh sang MODERATE
                if (moderateId.HasValue && infectedPlantIds.Count > 0)
                {
                    var infectedArray = infectedPlantIds.ToArray();
                    await dbContext.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE plant.plants SET current_health_level_id = {moderateId.Value}, last_inspected_at = NOW(), updated_at = NOW() WHERE farm_id = {request.FarmId} AND id = ANY({infectedArray})",
                        cancellationToken);
                }

                // Cập nhật cây không có bệnh sang HEALTHY
                var healthyPlantIds = farmPlants.Select(p => p.Id).Except(infectedPlantIds).ToArray();
                if (healthyId.HasValue && healthyPlantIds.Length > 0)
                {
                    await dbContext.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE plant.plants SET current_health_level_id = {healthyId.Value}, last_inspected_at = NOW(), updated_at = NOW() WHERE farm_id = {request.FarmId} AND id = ANY({healthyPlantIds})",
                        cancellationToken);
                }

                // 5. Đồng bộ hồ sơ bệnh án vào plant.plant_scans và plant.condition_detections
                await SyncPlantScansAndConditionsAsync(
                    request, farmPlants, observationsToInsert, infectedPlantIds,
                    moderateId, healthyId, cancellationToken);

                // 6. Chạy thuật toán phát hiện ổ dịch (High-Density Outbreak & Same-Disease Epidemic)
                await DetectAndSaveDiseaseOutbreakZonesAsync(
                    request, farmPlants, observationsToInsert, infectedPlantIds,
                    moderateId ?? Guid.Parse("11111111-1111-4111-8111-111111111104"), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save disease detections to database for Mission {MissionId}: {Message}",
                request.MissionId, ex.Message);
        }
    }

    private async Task SyncPlantScansAndConditionsAsync(
        DetectMissionDiseasesCommand request,
        List<PlantMatchTarget> farmPlants,
        List<MissionPlantObservation> observations,
        HashSet<Guid> infectedPlantIds,
        Guid? moderateId,
        Guid? healthyId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Xóa các lượt scan cũ của mission này để tránh trùng lặp
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM plant.condition_detections WHERE plant_scan_id IN (SELECT id FROM plant.plant_scans WHERE mission_id = {request.MissionId})",
                cancellationToken);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM plant.plant_scans WHERE mission_id = {request.MissionId}",
                cancellationToken);

            var conditions = await dbContext.Database.SqlQueryRaw<PlantConditionRow>(
                @"SELECT id AS ""Id"", code AS ""Code"", name AS ""Name"" FROM plant.plant_conditions")
                .ToListAsync(cancellationToken);

            var conditionMap = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            foreach (var cond in conditions)
            {
                conditionMap[cond.Code] = cond.Id;
            }

            var defaultConditionId = conditions.FirstOrDefault()?.Id ?? Guid.Empty;
            var defaultHealthyLevel = healthyId ?? Guid.Parse("11111111-1111-4111-8111-111111111102");
            var defaultModerateLevel = moderateId ?? Guid.Parse("11111111-1111-4111-8111-111111111104");

            // Nhóm quan sát theo cây (suggestedPlantId)
            var obsByPlant = observations
                .Where(o => o.SuggestedPlantId.HasValue)
                .GroupBy(o => o.SuggestedPlantId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var plant in farmPlants)
            {
                var isInfected = infectedPlantIds.Contains(plant.Id);
                var healthLevelId = isInfected ? defaultModerateLevel : defaultHealthyLevel;
                var scanId = Guid.NewGuid();

                // 1 bản ghi plant_scan cho mỗi cây trong đợt quét của mission
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $@"INSERT INTO plant.plant_scans (
                        id, farm_id, plant_id, mission_id, observed_at, source,
                        overall_health_level_id, created_at, server_received_at
                    ) VALUES (
                        {scanId}, {request.FarmId}, {plant.Id}, {request.MissionId}, NOW(), 'DRONE_AI'::system.scan_source,
                        {healthLevelId}, NOW(), NOW()
                    )",
                    cancellationToken);

                // Nếu cây có vết bệnh, thêm chi tiết vào condition_detections
                if (isInfected && obsByPlant.TryGetValue(plant.Id, out var plantObs))
                {
                    var groupedByClass = plantObs
                        .Where(o => o.MatchingParameters != null &&
                                    o.MatchingParameters.RootElement.TryGetProperty("class_name", out var pCls) &&
                                    !string.IsNullOrEmpty(pCls.GetString()))
                        .GroupBy(o => o.MatchingParameters!.RootElement.GetProperty("class_name").GetString()!);

                    foreach (var grp in groupedByClass)
                    {
                        var clsName = grp.Key;
                        if (clsName.StartsWith("Healthy", StringComparison.OrdinalIgnoreCase)) continue;

                        var conditionCode = MapClassToConditionCode(clsName);
                        var condId = conditionMap.TryGetValue(conditionCode, out var cId) ? cId : defaultConditionId;
                        if (condId == Guid.Empty) continue;

                        var detectionId = Guid.NewGuid();
                        var maxConf = grp.Max(o => o.DetectionConfidence);
                        var lesionCount = grp.Count();

                        await dbContext.Database.ExecuteSqlInterpolatedAsync(
                            $@"INSERT INTO plant.condition_detections (
                                id, plant_scan_id, condition_id, source, confidence, lesion_count, severity_level_id, created_at
                            ) VALUES (
                                {detectionId}, {scanId}, {condId}, 'AI'::system.finding_source, {maxConf}, {lesionCount}, {defaultModerateLevel}, NOW()
                            )",
                            cancellationToken);
                    }
                }
            }
            logger.LogInformation("Successfully synced plant_scans & condition_detections for Mission {MissionId}", request.MissionId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to sync plant scans and condition detections for Mission {MissionId}", request.MissionId);
        }
    }

    private async Task DetectAndSaveDiseaseOutbreakZonesAsync(
        DetectMissionDiseasesCommand request,
        List<PlantMatchTarget> farmPlants,
        List<MissionPlantObservation> observations,
        HashSet<Guid> infectedPlantIds,
        Guid moderateLevelId,
        CancellationToken cancellationToken)
    {
        try
        {
            if (infectedPlantIds.Count == 0) return;

            // Bán kính lân cận (khoảng cách 1-2 bước trụ thanh long: 4.5m)
            var zoneRadiusMeters = request.ZoneRadiusMeters is > 0 ? request.ZoneRadiusMeters.Value : 4.5;
            // Số cây bệnh tối thiểu để xác nhận là ổ dịch lây lan
            var minDiseasedTrees = request.MinDiseasedTrees is > 0 ? request.MinDiseasedTrees.Value : 3;

            // Xóa các disease_zones cũ được sinh bởi mission này
            var missionPrefix = $"mission_{request.MissionId:N}%";
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM plant.disease_zone_memberships WHERE disease_zone_id IN (SELECT id FROM plant.disease_zones WHERE farm_id = {request.FarmId} AND source_proposal_id LIKE {missionPrefix})",
                cancellationToken);
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM plant.disease_zones WHERE farm_id = {request.FarmId} AND source_proposal_id LIKE {missionPrefix}",
                cancellationToken);

            // Lấy ID phiên bản base map và boundary
            var baseMapRef = await dbContext.Database.SqlQueryRaw<BaseMapRefRow>(
                @"SELECT fbm.id AS ""BaseMapVersionId"", fb.id AS ""BoundaryVersionId""
                  FROM farm.farms f
                  LEFT JOIN farm.farm_boundaries fb ON fb.farm_id = f.id AND fb.status = 'APPROVED'
                  LEFT JOIN farm.farm_base_map_versions fbm ON fbm.farm_id = f.id AND fbm.status = 'PUBLISHED'
                  WHERE f.id = {0}
                  LIMIT 1",
                request.FarmId)
                .FirstOrDefaultAsync(cancellationToken);

            var boundaryVersionId = baseMapRef?.BoundaryVersionId ?? Guid.Empty;
            var baseMapVersionId = baseMapRef?.BaseMapVersionId ?? Guid.Empty;

            if (boundaryVersionId == Guid.Empty)
            {
                var fb = await dbContext.Database.SqlQueryRaw<GuidRow>(
                    @"SELECT id AS ""Value"" FROM farm.farm_boundaries WHERE farm_id = {0} ORDER BY (status = 'APPROVED') DESC, version_number DESC LIMIT 1",
                    request.FarmId).FirstOrDefaultAsync(cancellationToken);
                boundaryVersionId = fb?.Value ?? Guid.Empty;
            }
            if (baseMapVersionId == Guid.Empty)
            {
                var fbm = await dbContext.Database.SqlQueryRaw<GuidRow>(
                    @"SELECT id AS ""Value"" FROM farm.farm_base_map_versions WHERE farm_id = {0} ORDER BY (status = 'PUBLISHED') DESC, version_number DESC LIMIT 1",
                    request.FarmId).FirstOrDefaultAsync(cancellationToken);
                baseMapVersionId = fbm?.Value ?? Guid.Empty;
            }

            var surveyOrderInfo = await dbContext.Database.SqlQueryRaw<SurveyOrderRefRow>(
                @"SELECT dm.survey_order_id AS ""SurveyOrderId"", dm.tenant_id AS ""TenantId""
                  FROM mission.drone_missions dm
                  WHERE dm.id = {0}
                  LIMIT 1",
                request.MissionId)
                .FirstOrDefaultAsync(cancellationToken);

            var surveyOrderId = surveyOrderInfo?.SurveyOrderId;
            if (!surveyOrderId.HasValue || surveyOrderId == Guid.Empty)
            {
                var fallbackOrder = await dbContext.Database.SqlQueryRaw<GuidRow>(
                    @"SELECT id AS ""Value"" FROM survey.survey_orders WHERE farm_id = {0} LIMIT 1",
                    request.FarmId)
                    .FirstOrDefaultAsync(cancellationToken);
                surveyOrderId = fallbackOrder?.Value;
            }

            if (!surveyOrderId.HasValue || surveyOrderId == Guid.Empty)
            {
                logger.LogWarning("Cannot create disease_zones: No survey_order found for Farm {FarmId} / Mission {MissionId}",
                    request.FarmId, request.MissionId);
                return;
            }

            var surveyResultRef = await dbContext.Database.SqlQueryRaw<GuidRow>(
                @"SELECT id AS ""Value"" FROM survey.survey_results WHERE survey_order_id = {0} AND farm_id = {1} LIMIT 1",
                surveyOrderId.Value, request.FarmId)
                .FirstOrDefaultAsync(cancellationToken);

            var surveyResultId = surveyResultRef?.Value ?? Guid.Empty;
            if (surveyResultId == Guid.Empty)
            {
                surveyResultId = Guid.NewGuid();
                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $@"INSERT INTO survey.survey_results (
                        id, survey_order_id, tenant_id, farm_id, service_type, status, provenance, created_at, updated_at
                    ) VALUES (
                        {surveyResultId}, {surveyOrderId.Value}, {request.TenantId}, {request.FarmId},
                        'PLANT_HEALTH'::system.survey_service_type, 'PENDING_REVIEW'::system.survey_result_status,
                        '{{}}'::jsonb, NOW(), NOW()
                    )",
                    cancellationToken);
            }

            var conditions = await dbContext.Database.SqlQueryRaw<PlantConditionRow>(
                @"SELECT id AS ""Id"", code AS ""Code"", name AS ""Name"" FROM plant.plant_conditions")
                .ToListAsync(cancellationToken);
            var conditionMap = conditions.ToDictionary(c => c.Code, c => c.Id, StringComparer.OrdinalIgnoreCase);
            var defaultConditionId = conditions.FirstOrDefault()?.Id ?? Guid.Empty;

            // Danh sách các cây bị bệnh kèm danh sách bệnh phát hiện
            var plantDiseaseMap = new Dictionary<Guid, List<string>>();
            foreach (var obs in observations)
            {
                if (!obs.SuggestedPlantId.HasValue || !infectedPlantIds.Contains(obs.SuggestedPlantId.Value)) continue;
                var pId = obs.SuggestedPlantId.Value;
                if (!plantDiseaseMap.TryGetValue(pId, out var dList))
                {
                    dList = [];
                    plantDiseaseMap[pId] = dList;
                }
                if (obs.MatchingParameters != null &&
                    obs.MatchingParameters.RootElement.TryGetProperty("class_name", out var pCls))
                {
                    var cls = pCls.GetString();
                    if (!string.IsNullOrEmpty(cls) && !cls.StartsWith("Healthy", StringComparison.OrdinalIgnoreCase))
                    {
                        dList.Add(cls);
                    }
                }
            }

            var infectedPlants = farmPlants.Where(p => infectedPlantIds.Contains(p.Id)).ToList();

            // =========================================================================
            // TRƯỜNG HỢP 1: Ổ DỊCH MẬT ĐỘ CAO (HIGH-DENSITY OUTBREAK)
            // Gom cụm tất cả các cây bị bệnh bất kể bệnh gì
            // =========================================================================
            var highDensityClusters = FindConnectedComponents(infectedPlants, zoneRadiusMeters);
            var zoneIndex = 1;

            foreach (var cluster in highDensityClusters)
            {
                if (cluster.Count < minDiseasedTrees) continue;

                var centerLat = cluster.Average(p => p.Latitude);
                var centerLon = cluster.Average(p => p.Longitude);
                var maxDist = cluster.Max(p => CalculateHaversineMeters(centerLat, centerLon, p.Latitude, p.Longitude));
                var radius = Math.Round(Math.Max(maxDist + 1.5, 3.5), 1);

                // Thống kê phân bố bệnh trong cụm
                var breakdown = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in cluster)
                {
                    if (plantDiseaseMap.TryGetValue(p.Id, out var dList))
                    {
                        foreach (var d in dList)
                        {
                            breakdown[d] = breakdown.GetValueOrDefault(d, 0) + 1;
                        }
                    }
                }

                var zoneId = Guid.NewGuid();
                var zoneKey = Guid.NewGuid();
                var proposalId = $"mission_{request.MissionId:N}_high_density_{zoneIndex++}";

                var evidenceDoc = JsonSerializer.Serialize(new
                {
                    alert_type = "HIGH_DENSITY_OUTBREAK",
                    alert_title = "Ổ dịch mật độ cao",
                    total_trees = cluster.Count,
                    center = new { lat = centerLat, lon = centerLon },
                    radius_meters = radius,
                    disease_breakdown = breakdown
                });

                var recommendationDoc = JsonSerializer.Serialize(new[]
                {
                    new
                    {
                        treatment_type = "ISOLATION_AND_SPRAY",
                        action = "Khoanh vùng cách ly và phun khử khuẩn diện rộng",
                        note = $"Phát hiện cụm {cluster.Count} cây bệnh tập trung trong bán kính {radius}m"
                    }
                });

                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $@"INSERT INTO plant.disease_zones (
                        id, zone_key, version_number, tenant_id, farm_id,
                        survey_order_id, survey_result_id, farm_boundary_version_id, farm_base_map_version_id,
                        plant_condition_id, health_level_id, source_handoff_id, source_proposal_id,
                        proposed_geometry, recommendation_candidates, proposal_evidence,
                        current_membership_version, status, recommendations_rejected, created_at, updated_at
                    ) VALUES (
                        {zoneId}, {zoneKey}, 1, {request.TenantId}, {request.FarmId},
                        {surveyOrderId.Value}, {surveyResultId}, {boundaryVersionId}, {baseMapVersionId},
                        {defaultConditionId}, {moderateLevelId}, {request.MissionId}, {proposalId},
                        ST_Buffer(ST_SetSRID(ST_MakePoint({centerLon}, {centerLat}), 4326)::geography, {radius})::geometry,
                        {recommendationDoc}::jsonb, {evidenceDoc}::jsonb,
                        1, 'PROPOSED'::system.disease_zone_status, false, NOW(), NOW()
                    )",
                    cancellationToken);

                // Lưu các cây thành viên của ổ dịch
                foreach (var p in cluster)
                {
                    await dbContext.Database.ExecuteSqlInterpolatedAsync(
                        $@"INSERT INTO plant.disease_zone_memberships (
                            id, disease_zone_id, farm_id, plant_id, membership_version, kind, confidence, created_at
                        ) VALUES (
                            {Guid.NewGuid()}, {zoneId}, {request.FarmId}, {p.Id}, 1, 'PROPOSED'::system.disease_zone_membership_kind, 1.0, NOW()
                        )",
                        cancellationToken);
                }
            }

            // =========================================================================
            // TRƯỜNG HỢP 2: Ổ DỊCH LÂY LAN CÙNG MỘT BỆNH (SAME-DISEASE EPIDEMIC)
            // Nhóm riêng theo từng loại bệnh và gom cụm độc lập
            // =========================================================================
            var diseaseClasses = plantDiseaseMap.Values.SelectMany(v => v).Distinct().ToList();

            foreach (var cls in diseaseClasses)
            {
                var treesWithThisDisease = infectedPlants
                    .Where(p => plantDiseaseMap.TryGetValue(p.Id, out var dl) && dl.Contains(cls))
                    .ToList();

                if (treesWithThisDisease.Count < minDiseasedTrees) continue;

                var sameDiseaseClusters = FindConnectedComponents(treesWithThisDisease, zoneRadiusMeters);

                foreach (var cluster in sameDiseaseClusters)
                {
                    if (cluster.Count < minDiseasedTrees) continue;

                    var centerLat = cluster.Average(p => p.Latitude);
                    var centerLon = cluster.Average(p => p.Longitude);
                    var maxDist = cluster.Max(p => CalculateHaversineMeters(centerLat, centerLon, p.Latitude, p.Longitude));
                    var radius = Math.Round(Math.Max(maxDist + 1.5, 3.5), 1);

                    var condCode = MapClassToConditionCode(cls);
                    var condId = conditionMap.TryGetValue(condCode, out var cid) ? cid : defaultConditionId;
                    var viName = GetVietnameseDiseaseName(cls);

                    var zoneId = Guid.NewGuid();
                    var zoneKey = Guid.NewGuid();
                    var proposalId = $"mission_{request.MissionId:N}_same_disease_{cls}_{zoneIndex++}";

                    var evidenceDoc = JsonSerializer.Serialize(new
                    {
                        alert_type = "SAME_DISEASE_EPIDEMIC",
                        alert_title = $"Ổ dịch lây lan {viName}",
                        disease_name = cls,
                        disease_name_vi = viName,
                        total_trees = cluster.Count,
                        center = new { lat = centerLat, lon = centerLon },
                        radius_meters = radius,
                        disease_breakdown = new Dictionary<string, int> { [cls] = cluster.Count }
                    });

                    var recommendationDoc = JsonSerializer.Serialize(new[]
                    {
                        new
                        {
                            treatment_type = "TARGETED_FUNGICIDE",
                            action = $"Phun thuốc đặc trị bệnh {viName}",
                            note = $"Cảnh báo lây nhiễm chéo {cluster.Count} cây trong phạm vi {radius}m"
                        }
                    });

                    await dbContext.Database.ExecuteSqlInterpolatedAsync(
                        $@"INSERT INTO plant.disease_zones (
                            id, zone_key, version_number, tenant_id, farm_id,
                            survey_order_id, survey_result_id, farm_boundary_version_id, farm_base_map_version_id,
                            plant_condition_id, health_level_id, source_handoff_id, source_proposal_id,
                            proposed_geometry, recommendation_candidates, proposal_evidence,
                            current_membership_version, status, recommendations_rejected, created_at, updated_at
                        ) VALUES (
                            {zoneId}, {zoneKey}, 1, {request.TenantId}, {request.FarmId},
                            {surveyOrderId.Value}, {surveyResultId}, {boundaryVersionId}, {baseMapVersionId},
                            {condId}, {moderateLevelId}, {request.MissionId}, {proposalId},
                            ST_Buffer(ST_SetSRID(ST_MakePoint({centerLon}, {centerLat}), 4326)::geography, {radius})::geometry,
                            {recommendationDoc}::jsonb, {evidenceDoc}::jsonb,
                            1, 'PROPOSED'::system.disease_zone_status, false, NOW(), NOW()
                        )",
                        cancellationToken);

                    foreach (var p in cluster)
                    {
                        await dbContext.Database.ExecuteSqlInterpolatedAsync(
                            $@"INSERT INTO plant.disease_zone_memberships (
                                id, disease_zone_id, farm_id, plant_id, membership_version, kind, confidence, created_at
                            ) VALUES (
                                {Guid.NewGuid()}, {zoneId}, {request.FarmId}, {p.Id}, 1, 'PROPOSED'::system.disease_zone_membership_kind, 1.0, NOW()
                            )",
                            cancellationToken);
                    }
                }
            }
            logger.LogInformation("Successfully detected and saved outbreak zones for Mission {MissionId}", request.MissionId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to detect and save outbreak zones for Mission {MissionId}", request.MissionId);
        }
    }

    private static List<List<PlantMatchTarget>> FindConnectedComponents(
        List<PlantMatchTarget> nodes,
        double maxDistanceM)
    {
        var visited = new HashSet<Guid>();
        var components = new List<List<PlantMatchTarget>>();

        foreach (var node in nodes)
        {
            if (visited.Contains(node.Id)) continue;

            var cluster = new List<PlantMatchTarget>();
            var queue = new Queue<PlantMatchTarget>();
            queue.Enqueue(node);
            visited.Add(node.Id);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                cluster.Add(current);

                foreach (var neighbor in nodes)
                {
                    if (visited.Contains(neighbor.Id)) continue;
                    var dist = CalculateHaversineMeters(current.Latitude, current.Longitude, neighbor.Latitude, neighbor.Longitude);
                    if (dist <= maxDistanceM)
                    {
                        visited.Add(neighbor.Id);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            components.Add(cluster);
        }

        return components;
    }

    private static string MapClassToConditionCode(string className) => className switch
    {
        var s when s.Contains("Sun_Burn", StringComparison.OrdinalIgnoreCase) => "SUNBURN",
        var s when s.Contains("Brown_Spot", StringComparison.OrdinalIgnoreCase) => "BROWN_SPOT",
        var s when s.Contains("White_Spot", StringComparison.OrdinalIgnoreCase) => "BROWN_SPOT",
        var s when s.Contains("Anthracnose", StringComparison.OrdinalIgnoreCase) => "ANTHRACNOSE",
        var s when s.Contains("Soft_Rot", StringComparison.OrdinalIgnoreCase) => "ANTHRACNOSE",
        _ => "BROWN_SPOT"
    };

    private static string GetVietnameseDiseaseName(string className) => className switch
    {
        var s when s.Contains("Sun_Burn", StringComparison.OrdinalIgnoreCase) => "Cháy Nắng",
        var s when s.Contains("Brown_Spot", StringComparison.OrdinalIgnoreCase) => "Đốm Nâu",
        var s when s.Contains("Mechanical", StringComparison.OrdinalIgnoreCase) => "Vết Sẹo Cơ Học",
        var s when s.Contains("White_Spot", StringComparison.OrdinalIgnoreCase) => "Đốm Trắng",
        var s when s.Contains("Anthracnose", StringComparison.OrdinalIgnoreCase) => "Thán Thư",
        var s when s.Contains("Soft_Rot", StringComparison.OrdinalIgnoreCase) => "Thối Mềm",
        _ => className
    };

    private static double CalculateHaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371000;
        var dLat = (lat2 - lat1) * Math.PI / 180.0;
        var dLon = (lon2 - lon1) * Math.PI / 180.0;
        var a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);
        var c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        return r * c;
    }
}
