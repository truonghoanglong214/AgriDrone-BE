using System.IO;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.Modules.Missions.Domain.Processing;
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
    TimeProvider timeProvider,
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
    private sealed record CaptureMap(Guid BoundaryId, Guid BaseMapId);

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

        if (mission.Purpose != MissionPurpose.PlantHealth || mission.SurveyOrderId is null)
            return Result.Failure<DetectMissionDiseasesResult>(AppError.Conflict("DetectDiseases.InvalidPurpose", "An Order-bound Plant Health Mission is required."));
        var captureMap = await dbContext.Database.SqlQuery<CaptureMap>($"""
            SELECT bm.farm_boundary_version_id AS "BoundaryId", bm.id AS "BaseMapId"
            FROM farm.farm_base_map_versions bm
            WHERE bm.farm_id = {request.FarmId} AND bm.tenant_id = {request.TenantId}
              AND bm.farm_boundary_version_id = {mission.FarmBoundaryVersionId}
              AND bm.published_at IS NOT NULL
              AND (bm.id = {mission.SourceMapVersionId} OR EXISTS
                (SELECT 1 FROM farm.zone_map_versions zm WHERE zm.id = {mission.SourceMapVersionId} AND zm.farm_base_map_version_id = bm.id))
            """).SingleOrDefaultAsync(cancellationToken);
        if (captureMap is null)
            return Result.Failure<DetectMissionDiseasesResult>(AppError.Conflict("DetectDiseases.CaptureMapRequired", "The Mission must reference its published capture base map and approved boundary."));

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

        var startedAt = timeProvider.GetUtcNow();
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
        try
        {
            await SaveDiseaseDetectionsToDatabaseAsync(request, result, mediaList, captureMap, mission.EndedAt ?? mission.StartedAt, startedAt, cancellationToken);
        }
        catch (DiseaseDetectionLocationException error)
        {
            return Result.Failure<DetectMissionDiseasesResult>(AppError.Validation("DetectDiseases.InvalidLocation", error.Message));
        }
        catch (InvalidOperationException error)
        {
            return Result.Failure<DetectMissionDiseasesResult>(AppError.Conflict("DetectDiseases.ProvenanceRequired", error.Message));
        }

        return Result.Success(result);
    }

    private async Task SaveDiseaseDetectionsToDatabaseAsync(
        DetectMissionDiseasesCommand request,
        DetectMissionDiseasesResult result,
        List<MissionMedia> mediaList,
        CaptureMap captureMap,
        DateTimeOffset? missionObservedAt,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        try
        {
            if (result.Detections.Any(d => string.IsNullOrWhiteSpace(d.Id) || string.IsNullOrWhiteSpace(d.ClassName) ||
                !double.IsFinite(d.Lat) || !double.IsFinite(d.Lon) || d.Lat is < -90 or > 90 || d.Lon is < -180 or > 180 ||
                !double.IsFinite(d.Confidence) || d.Confidence is < 0 or > 1) ||
                result.Detections.Select(d => (d.ImageName, d.Id)).Distinct().Count() != result.Detections.Count)
                throw new InvalidOperationException("Detection output needs unique source IDs, valid coordinates, classes and confidence.");
            var model = await dbContext.AiModelVersions.AsNoTracking().SingleOrDefaultAsync(m =>
                m.IsActive && m.ModelType == AiModelType.DiseaseDetection && m.ModelName == result.ModelName, cancellationToken)
                ?? throw new InvalidOperationException("Register the exact model name returned by the detector before publishing Plant Health output.");
            var metrics = model.Metrics.RootElement;
            string RequiredMetadata(string name) => metrics.TryGetProperty(name, out var field) && field.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(field.GetString()) ? field.GetString()! :
                throw new InvalidOperationException($"The model registry is missing provenance metadata '{name}'.");
            var provenance = new PlantHealthProvenanceV4(RequiredMetadata("datasetId"), RequiredMetadata("datasetVersion"),
                RequiredMetadata("annotationRuleVersion"), model.Id, model.Version,
                "confidence:" + request.ConfidenceThreshold.ToString("R", CultureInfo.InvariantCulture), RequiredMetadata("boundaryPolicyVersion"));
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            _ = await dbContext.DroneMissions.FromSqlInterpolated($"SELECT *, xmin FROM mission.drone_missions WHERE id = {request.MissionId} FOR UPDATE")
                .SingleAsync(cancellationToken);
            var version = (await dbContext.AiProcessingJobs.Where(j => j.MissionId == request.MissionId && j.JobType == AiJobType.DiseaseDetection)
                .Select(j => (int?)j.AttemptNumber).MaxAsync(cancellationToken) ?? 0) + 1;
            var snapshot = new PlantHealthProcessingSnapshot(version, captureMap.BoundaryId, captureMap.BaseMapId, provenance, RequiredMetadata("teamLabelLimitation"));
            var job = AiProcessingJob.RecordCompletedHealthRun(request.MissionId, model.Id, snapshot,
                JsonSerializer.SerializeToDocument(new { request.ConfidenceThreshold, captureMap, MediaIds = mediaList.Select(m => m.MediaId).ToArray() }),
                JsonSerializer.SerializeToDocument(result), startedAt, timeProvider.GetUtcNow());
            dbContext.AiProcessingJobs.Add(job);
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

            // Retain every processing run and its observations; reprocess never overwrites history.

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
                    source_finding_id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(
                        $"{request.MissionId:D}/{detection.ImageName}/{detection.Id}" )).AsSpan(0, 16)),
                    observed_at = mediaList.FirstOrDefault(m => Path.GetFileName(m.Media.StorageKey) == detection.ImageName)?.CapturedAt ?? missionObservedAt
                        ?? throw new InvalidOperationException("Capture time or Mission flight time is required for observed-time provenance."),
                    suggested_plant_id = matchedPlantId,
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
                    resolvedPlantId: null,
                    gpsDistanceM: GetGpsDistanceMeters(nearestPlant is null ? null : minDistance, detection.ImageName),
                    matchingParameters: metadataDoc,
                    evidenceMediaId: evidenceMediaId,
                    reviewStatus: ObservationReviewStatus.Pending,
                    aiJobId: job.Id,
                    modelVersionId: model.Id,
                    matchConfidence: matchConf,
                    selectedMatchStrategy: MatchStrategy.GpsOnly,
                    matchingAlgorithmVersion: model.Version);

                observationsToInsert.Add(observation);
            }

            await dbContext.MissionPlantObservations.AddRangeAsync(observationsToInsert, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            if (observationsToInsert.Count > 0)
            {
                logger.LogInformation(
                    "Successfully saved {Count} disease observations to database for Mission {MissionId} ({Infected} plants infected). Ready for publishing via plant-health/publish.",
                    observationsToInsert.Count, request.MissionId, infectedPlantIds.Count);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save disease detections to database for Mission {MissionId}: {Message}",
                request.MissionId, ex.Message);
            throw;
        }
    }

    internal static decimal? GetGpsDistanceMeters(double? distance, string imageName)
    {
        if (distance is null)
        {
            return null;
        }

        // PostgreSQL numeric(8,3) cannot store 100,000m, including after rounding.
        if (!double.IsFinite(distance.Value) || distance.Value < 0 ||
            Math.Round(distance.Value, 3, MidpointRounding.AwayFromZero) >= 100_000)
        {
            throw new DiseaseDetectionLocationException(imageName);
        }

        return (decimal)Math.Round(distance.Value, 3, MidpointRounding.AwayFromZero);
    }

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
