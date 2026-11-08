using System.Text.Json;
using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AgriDrone.Modules.Missions.Application.Features.Processing.GetFarmPlantHealthMap;

internal sealed class GetFarmPlantHealthMapQueryHandler(
    MissionsDbContext dbContext,
    IObjectStorage objectStorage,
    ILogger<GetFarmPlantHealthMapQueryHandler> logger)
    : IRequestHandler<GetFarmPlantHealthMapQuery, Result<FarmPlantHealthMapResult>>
{
    private static readonly Dictionary<string, string> ClassTranslations = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Disease_Sun_Burn"] = "Cháy Nắng (Sun Burn)",
        ["Disease_White_Spot"] = "Đốm Trắng (White Spot)",
        ["Disease_Soft_Rot"] = "Thối Nhũn (Soft Rot)",
        ["Disease_Brown_Spot"] = "Đốm Nâu (Brown Spot)",
        ["Disease_Anthracnose"] = "Thán Thư (Anthracnose)",
        ["Healthy_Fruit_Green"] = "Quả Thanh Long Xanh Khỏe",
        ["Healthy_Fruit_Damaged"] = "Quả Thanh Long Vết Nhẹ/Lành"
    };

    private sealed record BaseMapRow(
        Guid Id,
        Guid TenantId,
        Guid SourceMissionId,
        string Status,
        double? GridBearingDeg,
        double? PlantSpacingM,
        double? RowSpacingM);

    private sealed record PlantRow(
        Guid Id,
        string PlantCode,
        int? RowIndex,
        int? ColumnIndex,
        double Latitude,
        double Longitude,
        string? LifecycleStatus);

    private sealed record ObservationRow(
        Guid Id,
        Guid MissionId,
        Guid? SuggestedPlantId,
        decimal? DetectionConfidence,
        decimal? GpsDistanceM,
        string? MatchingParametersJson);

    private sealed record DiseaseZoneRow(
        Guid Id,
        Guid ZoneKey,
        string Status,
        string? GeometryGeoJson,
        string? ProposalEvidenceJson,
        string? RecommendationsJson,
        string? ConditionCode,
        string? ConditionName);

    private sealed record DiseaseZoneMemberRow(
        Guid ZoneId,
        Guid PlantId,
        string PlantCode,
        int? RowIndex,
        int? ColumnIndex,
        double Latitude,
        double Longitude);

    public async Task<Result<FarmPlantHealthMapResult>> Handle(
        GetFarmPlantHealthMapQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var latestResultWithdrawn = await dbContext.Database.SqlQueryRaw<bool>(
                @"SELECT (invalidated_at IS NOT NULL) AS ""Value""
                  FROM survey.survey_results
                  WHERE farm_id = {0}
                    AND service_type = 'PLANT_HEALTH'::system.survey_service_type
                    AND status = 'PUBLISHED'::system.survey_result_status
                  ORDER BY published_at DESC
                  LIMIT 1",
                request.FarmId)
                .FirstOrDefaultAsync(cancellationToken);
            if (latestResultWithdrawn)
            {
                return Result.Failure<FarmPlantHealthMapResult>(AppError.Conflict(
                    "FarmPlantHealthMap.ResultWithdrawn",
                    "The published Plant Health result was withdrawn after a verified owner report."));
            }

            // 1. Lấy thông tin bản đồ nền đã công bố của Farm
            var baseMapRow = await dbContext.Database.SqlQueryRaw<BaseMapRow>(
                @"SELECT id AS ""Id"", tenant_id AS ""TenantId"", source_mission_id AS ""SourceMissionId"",
                         status::text AS ""Status"", grid_bearing_deg AS ""GridBearingDeg"",
                         plant_spacing_m AS ""PlantSpacingM"", row_spacing_m AS ""RowSpacingM""
                  FROM farm.farm_base_map_versions
                  WHERE farm_id = {0}
                  ORDER BY CASE WHEN status = 'PUBLISHED' THEN 1 ELSE 2 END, created_at DESC
                  LIMIT 1",
                request.FarmId)
                .FirstOrDefaultAsync(cancellationToken);

            var resolvedTenantId = request.TenantId ?? baseMapRow?.TenantId ?? Guid.Empty;

            // Sinh URL MinIO cho map_preview.jpg
            string? previewUrl = null;
            if (baseMapRow is not null && resolvedTenantId != Guid.Empty)
            {
                try
                {
                    var storageUri = $"minio://agridrone-media/tenants/{resolvedTenantId:D}/media/{baseMapRow.SourceMissionId:D}/map_preview.jpg";
                    var uri = await objectStorage.CreateDownloadUriAsync(
                        storageUri,
                        TimeSpan.FromHours(24),
                        cancellationToken);
                    previewUrl = uri.ToString();
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to create presigned download URI for base map preview");
                    previewUrl = $"http://localhost:9000/agridrone-media/tenants/{resolvedTenantId:D}/media/{baseMapRow.SourceMissionId:D}/map_preview.jpg";
                }
            }

            // 2. Lấy danh sách toàn bộ trụ cây thuộc Farm
            var plantRows = await dbContext.Database.SqlQueryRaw<PlantRow>(
                @"SELECT id AS ""Id"", plant_code AS ""PlantCode"",
                         row_index AS ""RowIndex"", column_index AS ""ColumnIndex"",
                         ST_Y(location::geometry) AS ""Latitude"",
                         ST_X(location::geometry) AS ""Longitude"",
                         lifecycle_status::text AS ""LifecycleStatus""
                  FROM plant.plants
                  WHERE farm_id = {0}
                  ORDER BY row_index, column_index",
                request.FarmId)
                .ToListAsync(cancellationToken);

            // 3. Lấy toàn bộ quan sát bệnh & quả thuộc Farm
            var obsRows = await dbContext.Database.SqlQueryRaw<ObservationRow>(
                @"SELECT id AS ""Id"", mission_id AS ""MissionId"",
                         suggested_plant_id AS ""SuggestedPlantId"",
                         detection_confidence AS ""DetectionConfidence"",
                         gps_distance_m AS ""GpsDistanceM"",
                         matching_parameters::text AS ""MatchingParametersJson""
                  FROM mission.mission_plant_observations
                  WHERE farm_id = {0}
                    AND review_status::text <> 'REJECTED'
                  ORDER BY created_at DESC",
                request.FarmId)
                .ToListAsync(cancellationToken);

            // Nhóm quan sát theo suggested_plant_id
            var obsByPlant = new Dictionary<Guid, List<PlantObservationItemResult>>();
            var diseaseSummary = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var obs in obsRows)
            {
                var className = "Unknown";
                string? imageName = null;
                string? cropImageUrl = null;
                string? annotatedImageUrl = null;
                string? detectionId = null;

                if (!string.IsNullOrWhiteSpace(obs.MatchingParametersJson))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(obs.MatchingParametersJson);
                        var root = doc.RootElement;
                        if (root.TryGetProperty("class_name", out var pCls)) className = pCls.GetString() ?? "Unknown";
                        if (root.TryGetProperty("image_name", out var pImg)) imageName = pImg.GetString();
                        if (root.TryGetProperty("crop_image_url", out var pCrop)) cropImageUrl = pCrop.GetString();
                        if (root.TryGetProperty("annotated_image_url", out var pAnn)) annotatedImageUrl = pAnn.GetString();
                        if (root.TryGetProperty("detection_id", out var pDet)) detectionId = pDet.GetString();
                    }
                    catch (Exception jsonEx)
                    {
                        logger.LogWarning(jsonEx, "Failed to parse matching_parameters JSON for observation {ObsId}", obs.Id);
                    }
                }

                diseaseSummary[className] = diseaseSummary.GetValueOrDefault(className, 0) + 1;

                var obsItem = new PlantObservationItemResult(
                    Id: obs.Id,
                    MissionId: obs.MissionId,
                    ClassName: className,
                    ClassNameVi: ClassTranslations.GetValueOrDefault(className, className),
                    Confidence: (double)(obs.DetectionConfidence ?? 0m),
                    GpsDistanceM: (double)(obs.GpsDistanceM ?? 0m),
                    ImageName: imageName,
                    CropImageUrl: cropImageUrl,
                    AnnotatedImageUrl: annotatedImageUrl,
                    DetectionId: detectionId);

                if (obs.SuggestedPlantId.HasValue)
                {
                    var pId = obs.SuggestedPlantId.Value;
                    if (!obsByPlant.TryGetValue(pId, out var list))
                    {
                        list = [];
                        obsByPlant[pId] = list;
                    }
                    list.Add(obsItem);
                }
            }

            // 4. Tổng hợp trạng thái 3 nhóm màu cho từng cây
            var normalCount = 0;
            var healthyFruitCount = 0;
            var diseaseCount = 0;
            var plantResults = new List<PlantHealthItemResult>(plantRows.Count);

            foreach (var plant in plantRows)
            {
                var pObservations = obsByPlant.GetValueOrDefault(plant.Id, []);

                var hasDisease = pObservations.Any(o => !o.ClassName.StartsWith("Healthy", StringComparison.OrdinalIgnoreCase));
                var hasHealthy = pObservations.Any(o => o.ClassName.StartsWith("Healthy", StringComparison.OrdinalIgnoreCase));

                string healthStatus;
                if (hasDisease)
                {
                    healthStatus = "DISEASE";
                    diseaseCount++;
                }
                else if (hasHealthy)
                {
                    healthStatus = "HEALTHY_FRUIT";
                    healthyFruitCount++;
                }
                else
                {
                    healthStatus = "NORMAL";
                    normalCount++;
                }

                plantResults.Add(new PlantHealthItemResult(
                    Id: plant.Id,
                    PlantCode: plant.PlantCode,
                    Row: plant.RowIndex ?? 0,
                    Col: plant.ColumnIndex ?? 0,
                    Lat: plant.Latitude,
                    Lon: plant.Longitude,
                    LifecycleStatus: plant.LifecycleStatus ?? "ACTIVE",
                    HealthStatus: healthStatus,
                    Observations: pObservations));
            }

            // 5. Tọa độ ranh giới bao phủ (bounds): Ưu tiên lấy trực tiếp từ metadata.json của GeoTIFF gốc
            MapBoundsResult? bounds = null;
            if (baseMapRow is not null && resolvedTenantId != Guid.Empty)
            {
                try
                {
                    var metaUri = $"minio://agridrone-media/tenants/{resolvedTenantId:D}/media/{baseMapRow.SourceMissionId:D}/metadata.json";
                    await objectStorage.ReadAsync(metaUri, async (stream, ct) =>
                    {
                        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                        if (doc.RootElement.TryGetProperty("bounds", out var b))
                        {
                            var minLat = b.GetProperty("min_lat").GetDouble();
                            var maxLat = b.GetProperty("max_lat").GetDouble();
                            var minLon = b.GetProperty("min_lon").GetDouble();
                            var maxLon = b.GetProperty("max_lon").GetDouble();
                            bounds = new MapBoundsResult(minLat, maxLat, minLon, maxLon);
                        }
                    }, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to read orthophoto bounds from metadata.json for mission {MissionId}", baseMapRow.SourceMissionId);
                }
            }

            if (bounds is null && plantRows.Count > 0)
            {
                bounds = new MapBoundsResult(
                    MinLat: plantRows.Min(p => p.Latitude) - 0.0002,
                    MaxLat: plantRows.Max(p => p.Latitude) + 0.0002,
                    MinLon: plantRows.Min(p => p.Longitude) - 0.0002,
                    MaxLon: plantRows.Max(p => p.Longitude) + 0.0002);
            }

            // 6. Lấy ranh giới trang trại đã duyệt (APPROVED boundary)
            string? boundaryGeoJson = null;
            try
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
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load approved boundary GeoJSON for farm {FarmId}", request.FarmId);
            }

            BaseMapInfoResult? baseMapInfo = null;
            if (baseMapRow is not null)
            {
                baseMapInfo = new BaseMapInfoResult(
                    SourceMissionId: baseMapRow.SourceMissionId,
                    Status: baseMapRow.Status,
                    PreviewUrl: previewUrl,
                    Bounds: bounds,
                    GridBearingDeg: baseMapRow.GridBearingDeg ?? 0.0,
                    PlantSpacingM: baseMapRow.PlantSpacingM ?? 2.7,
                    RowSpacingM: baseMapRow.RowSpacingM ?? 2.8);
            }

            var summary = new PlantHealthSummaryResult(
                TotalPlants: plantResults.Count,
                NormalPlants: normalCount,
                HealthyFruitPlants: healthyFruitCount,
                DiseasePlants: diseaseCount,
                DiseaseBreakdown: diseaseSummary);

            // 7. Lấy danh sách Ổ dịch (Disease Zones) của Farm
            var zoneResults = new List<DiseaseZoneItemResult>();
            try
            {
                var zoneRows = await dbContext.Database.SqlQueryRaw<DiseaseZoneRow>(
                    @"SELECT z.id AS ""Id"", z.zone_key AS ""ZoneKey"",
                             z.status::text AS ""Status"",
                             ST_AsGeoJSON(COALESCE(z.reviewed_geometry, z.proposed_geometry)) AS ""GeometryGeoJson"",
                             z.proposal_evidence::text AS ""ProposalEvidenceJson"",
                             z.recommendation_candidates::text AS ""RecommendationsJson"",
                             pc.code AS ""ConditionCode"", pc.name AS ""ConditionName""
                      FROM plant.disease_zones z
                      LEFT JOIN plant.plant_conditions pc ON pc.id = z.plant_condition_id
                      WHERE z.farm_id = {0} AND z.status != 'REJECTED' AND z.status != 'SUPERSEDED'
                        AND NOT EXISTS (
                          SELECT 1 FROM survey.survey_results sr
                          WHERE sr.id = z.survey_result_id AND sr.invalidated_at IS NOT NULL)
                      ORDER BY z.created_at DESC",
                    request.FarmId)
                    .ToListAsync(cancellationToken);

                if (zoneRows.Count > 0)
                {
                    var memberRows = await dbContext.Database.SqlQueryRaw<DiseaseZoneMemberRow>(
                        @"SELECT m.disease_zone_id AS ""ZoneId"", m.plant_id AS ""PlantId"",
                                 p.plant_code AS ""PlantCode"", p.row_index AS ""RowIndex"", p.column_index AS ""ColumnIndex"",
                                 ST_Y(p.location::geometry) AS ""Latitude"", ST_X(p.location::geometry) AS ""Longitude""
                          FROM plant.disease_zone_memberships m
                          JOIN plant.plants p ON p.id = m.plant_id
                          WHERE m.farm_id = {0}",
                        request.FarmId)
                        .ToListAsync(cancellationToken);

                    var membersByZone = memberRows
                        .GroupBy(m => m.ZoneId)
                        .ToDictionary(g => g.Key, g => g.ToList());

                    foreach (var z in zoneRows)
                    {
                        var alertType = "HIGH_DENSITY_OUTBREAK";
                        string? diseaseName = null;
                        string? diseaseNameVi = null;
                        var totalTrees = 0;
                        var centerLat = 0.0;
                        var centerLon = 0.0;
                        var radiusMeters = 0.0;
                        var breakdown = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                        if (!string.IsNullOrEmpty(z.ProposalEvidenceJson))
                        {
                            try
                            {
                                using var doc = JsonDocument.Parse(z.ProposalEvidenceJson);
                                var root = doc.RootElement;
                                if (root.TryGetProperty("alert_type", out var pType)) alertType = pType.GetString() ?? alertType;
                                if (root.TryGetProperty("disease_name", out var pDName)) diseaseName = pDName.GetString();
                                if (root.TryGetProperty("disease_name_vi", out var pDVi)) diseaseNameVi = pDVi.GetString();
                                if (root.TryGetProperty("total_trees", out var pTot)) totalTrees = pTot.GetInt32();
                                if (root.TryGetProperty("radius_meters", out var pRad)) radiusMeters = pRad.GetDouble();
                                if (root.TryGetProperty("center", out var pCenter))
                                {
                                    if (pCenter.TryGetProperty("lat", out var pLat)) centerLat = pLat.GetDouble();
                                    if (pCenter.TryGetProperty("lon", out var pLon)) centerLon = pLon.GetDouble();
                                }
                                if (root.TryGetProperty("disease_breakdown", out var pBk) && pBk.ValueKind == JsonValueKind.Object)
                                {
                                    foreach (var prop in pBk.EnumerateObject())
                                    {
                                        breakdown[prop.Name] = prop.Value.GetInt32();
                                    }
                                }
                            }
                            catch
                            {
                            }
                        }

                        var treeItems = membersByZone.TryGetValue(z.Id, out var mList)
                            ? mList.Select(m => new DiseaseZoneTreeItemResult(m.PlantId, m.PlantCode, m.RowIndex ?? 0, m.ColumnIndex ?? 0, m.Latitude, m.Longitude)).ToList()
                            : new List<DiseaseZoneTreeItemResult>();

                        if (totalTrees == 0) totalTrees = treeItems.Count;
                        if (centerLat == 0.0 && treeItems.Count > 0)
                        {
                            centerLat = treeItems.Average(t => t.Lat);
                            centerLon = treeItems.Average(t => t.Lon);
                        }

                        zoneResults.Add(new DiseaseZoneItemResult(
                            Id: z.Id,
                            ZoneKey: z.ZoneKey,
                            Status: z.Status,
                            AlertType: alertType,
                            DiseaseName: diseaseName ?? z.ConditionCode,
                            DiseaseNameVi: diseaseNameVi ?? z.ConditionName,
                            TotalTrees: totalTrees,
                            CenterLat: centerLat,
                            CenterLon: centerLon,
                            RadiusMeters: radiusMeters,
                            GeometryGeoJson: z.GeometryGeoJson,
                            DiseaseBreakdown: breakdown,
                            Trees: treeItems));
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load disease zones for farm {FarmId}", request.FarmId);
            }

            return Result.Success(new FarmPlantHealthMapResult(
                FarmId: request.FarmId,
                TenantId: resolvedTenantId,
                BaseMap: baseMapInfo,
                Summary: summary,
                Plants: plantResults,
                BoundaryGeoJson: boundaryGeoJson,
                Zones: zoneResults));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get plant health map for farm {FarmId}", request.FarmId);
            return Result.Failure<FarmPlantHealthMapResult>(AppError.Failure(
                "FarmPlantHealthMap.QueryFailed",
                $"Lỗi truy vấn dữ liệu sức khỏe cây cho nông trại: {ex.Message}"));
        }
    }
}

