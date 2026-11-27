using System.IO;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.Modules.Missions.Domain.Processing;
using AgriDrone.Modules.Missions.Application.Abstractions.Media;
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
    IObjectStorage objectStorage,
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
        int? ColumnIndex,
        Guid? PlantProfileId);
    private sealed record CaptureMap(Guid BoundaryId, Guid BaseMapId);
    private sealed record ReferenceRow(Guid Id);
    private sealed record OrderBaselineRow(Guid? BaselineMapVersionId);
    private sealed record OrderLockRow(Guid Id, string Status, Guid? BaselineMapVersionId);
    private sealed record CommercialPolicyRow(Guid PolicyVersionId);
    private sealed record ApprovalRow(Guid Id, Guid ManifestId, string ManifestHash, Guid ProcessingJobId);
    private sealed record CommercialSnapshotRow(string ServiceCode, bool ScreensDisease, Guid BusinessPolicyVersionId);
    private sealed record AttemptRow(Guid Id, string? InputApprovalReference, string? ProcessingJobReference, Guid? MappingSourceAttemptId);
    private sealed record AttemptLockRow(Guid Id, string Status, string? InputApprovalReference, string? ProcessingJobReference, Guid? MappingSourceAttemptId);
    private sealed record FrozenFileRow(Guid MediaAssetId, string Sha256, long SizeBytes, string MediaRole);
    private sealed record SnapshotRow(Guid Id, Guid MappingCandidateBatchId);
    private sealed record MappingBatchRow(Guid Id, string MappingKind, string Status);
    private sealed record ComboInferencePolicy(
        Guid ModelVersionId, string Version, double Confidence, double ClusterRadiusMeters, double BufferMeters);

    private sealed record V3ComboInferenceContext(
        Guid OrderId,
        Guid AttemptId,
        Guid? MappingSourceAttemptId,
        ApprovalRow Approval,
        List<FrozenFileRow> FrozenFiles,
        Guid SnapshotId,
        Guid MappingBatchId,
        AgriDrone.IntegrationContracts.Mapping.MappingKind MappingKind,
        CaptureMap CaptureMap,
        ComboInferencePolicy Policy);

    private static readonly Guid ComboAnalysisNamespace = new("e2c56a81-79b8-4e1b-93f4-3d0cf3b12345");

    private Task<SnapshotRow?> ReadVerifiedMappingSnapshotAsync(
        Guid orderId, Guid tenantId, Guid farmId, Guid missionId, Guid attemptId,
        Guid? mappingSourceAttemptId, CaptureMap map, CancellationToken cancellationToken) =>
        dbContext.Database.SqlQuery<SnapshotRow>($"""
            SELECT snapshot.id AS "Id", snapshot.mapping_candidate_batch_id AS "MappingCandidateBatchId"
            FROM survey.survey_orders AS orders
            JOIN survey.survey_order_pole_count_snapshots AS snapshot
                ON snapshot.id = orders.verified_pole_count_snapshot_id AND snapshot.survey_order_id = orders.id
            JOIN survey.capture_attempts AS source
                ON source.id = snapshot.capture_attempt_id AND source.order_id = orders.id
                AND source.tenant_id = snapshot.tenant_id AND source.farm_id = snapshot.farm_id
            JOIN mission.drone_missions AS map_mission
                ON map_mission.id = snapshot.mission_id AND map_mission.capture_attempt_id = source.id
                AND map_mission.survey_order_id = orders.id
                AND map_mission.tenant_id = snapshot.tenant_id AND map_mission.farm_id = snapshot.farm_id
            WHERE orders.id = {orderId}
              AND snapshot.tenant_id = {tenantId} AND snapshot.farm_id = {farmId}
              AND snapshot.capture_attempt_id = {mappingSourceAttemptId ?? attemptId}
              AND snapshot.farm_base_map_version_id = {map.BaseMapId}
              AND snapshot.farm_boundary_version_id = {map.BoundaryId}
              AND (({mappingSourceAttemptId != null} = FALSE AND snapshot.mission_id = {missionId})
                 OR ({mappingSourceAttemptId != null} = TRUE AND snapshot.mission_id <> {missionId}
                     AND source.status = 'ACCEPTED' AND source.mapping_source_attempt_id IS NULL
                     AND source.processing_acceptance_receipt = snapshot.id::text))
            """).SingleOrDefaultAsync(cancellationToken);

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

        if (mission.SurveyOrderId is not Guid orderId)
            return Result.Failure<DetectMissionDiseasesResult>(AppError.Conflict("DetectDiseases.OrderRequired", "An Order-bound Mission is required for disease detection."));

        CaptureMap? captureMap = null;
        bool isV3Combo = false;
        V3ComboInferenceContext? v3Context = null;

        if (mission.Purpose == MissionPurpose.Capture)
        {
            var commercial = await dbContext.Database.SqlQuery<CommercialSnapshotRow>($"""
                SELECT service_code AS "ServiceCode", screens_disease AS "ScreensDisease", business_policy_version_id AS "BusinessPolicyVersionId"
                FROM survey.order_commercial_snapshots
                WHERE survey_order_id = {orderId}
                """).SingleOrDefaultAsync(cancellationToken);

            if (commercial is null || commercial.ServiceCode != "COMBO" || !commercial.ScreensDisease)
            {
                return Result.Failure<DetectMissionDiseasesResult>(AppError.Conflict(
                    "DetectDiseases.InvalidPurpose", "Only COMBO Capture missions with ScreensDisease enabled can run disease detection."));
            }

            var mapId = mission.InternallyApprovedMapVersionId ?? mission.SourceMapVersionId;
            if (mapId is null)
            {
                return Result.Failure<DetectMissionDiseasesResult>(AppError.Conflict(
                    "DetectDiseases.CaptureMapRequired", "A referenced capture map is required."));
            }

            captureMap = await dbContext.Database.SqlQuery<CaptureMap>($"""
                SELECT bm.farm_boundary_version_id AS "BoundaryId", bm.id AS "BaseMapId"
                FROM farm.farm_base_map_versions bm
                WHERE bm.farm_id = {request.FarmId} AND bm.tenant_id = {request.TenantId}
                  AND bm.farm_boundary_version_id = {mission.FarmBoundaryVersionId}
                  AND bm.status IN ('INTERNAL_APPROVED', 'PUBLISHED')
                  AND bm.id = {mapId}
                """).SingleOrDefaultAsync(cancellationToken);

            if (captureMap is null)
            {
                return Result.Failure<DetectMissionDiseasesResult>(AppError.Conflict(
                    "DetectDiseases.CaptureMapRequired", "A valid internally approved or published capture map and boundary are required."));
            }

            var attemptRow = await dbContext.Database.SqlQuery<AttemptRow>($"""
                SELECT id AS "Id", input_approval_reference AS "InputApprovalReference", processing_job_reference AS "ProcessingJobReference",
                       mapping_source_attempt_id AS "MappingSourceAttemptId"
                FROM survey.capture_attempts
                WHERE id = {mission.CaptureAttemptId} AND order_id = {orderId} AND status = 'PROCESSING'
                """).SingleOrDefaultAsync(cancellationToken);

            if (attemptRow is null || string.IsNullOrWhiteSpace(attemptRow.InputApprovalReference))
            {
                return Result.Failure<DetectMissionDiseasesResult>(AppError.Conflict(
                    "DetectDiseases.ApprovalRequired", "An approved capture input with valid approval reference is required."));
            }

            if (!Guid.TryParse(attemptRow.InputApprovalReference, out var approvalId))
            {
                return Result.Failure<DetectMissionDiseasesResult>(AppError.Conflict(
                    "DetectDiseases.ApprovalInvalid", "Capture attempt input approval reference is not a valid GUID."));
            }

            var preApproval = await dbContext.Database.SqlQuery<ApprovalRow>($"""
                SELECT id AS "Id", input_manifest_id AS "ManifestId", manifest_fingerprint AS "ManifestHash", processing_job_id AS "ProcessingJobId"
                FROM survey.capture_input_approvals
                WHERE id = {approvalId} AND capture_attempt_id = {mission.CaptureAttemptId}
                """).SingleOrDefaultAsync(cancellationToken);

            if (preApproval is null)
            {
                return Result.Failure<DetectMissionDiseasesResult>(AppError.Conflict(
                    "DetectDiseases.ApprovalRequired", "Approved capture input record was not found for this attempt."));
            }

            var frozenFiles = await dbContext.Database.SqlQuery<FrozenFileRow>($"""
                SELECT media_asset_id AS "MediaAssetId", sha256 AS "Sha256", size_bytes AS "SizeBytes", media_role AS "MediaRole"
                FROM survey.capture_input_manifest_files
                WHERE manifest_id = {preApproval.ManifestId} AND media_role IN ('RawImage', 'RAW_IMAGE')
                """).ToListAsync(cancellationToken);

            if (frozenFiles.Count == 0)
            {
                return Result.Failure<DetectMissionDiseasesResult>(AppError.Conflict(
                    "DetectDiseases.FrozenFilesMissing", "No frozen raw images found in approved capture manifest."));
            }

            var approvedSnapshot = await ReadVerifiedMappingSnapshotAsync(
                orderId, request.TenantId, request.FarmId, mission.Id, attemptRow.Id,
                attemptRow.MappingSourceAttemptId, captureMap, cancellationToken);

            if (approvedSnapshot is null)
            {
                return Result.Failure<DetectMissionDiseasesResult>(AppError.Conflict(
                    "DetectDiseases.PoleCountSnapshotRequired", "An approved pole count snapshot matching order, attempt, mission, base map and boundary is required."));
            }

            var mappingBatchRow = await dbContext.Database.SqlQuery<MappingBatchRow>($"""
                SELECT id AS "Id", mapping_kind AS "MappingKind", status AS "Status"
                FROM farm.mapping_candidate_batches
                WHERE id = {approvedSnapshot.MappingCandidateBatchId} AND tenant_id = {request.TenantId} AND farm_id = {request.FarmId}
                """).SingleOrDefaultAsync(cancellationToken);

            if (mappingBatchRow is null || mappingBatchRow.Status is not ("Approved" or "Applied"))
            {
                return Result.Failure<DetectMissionDiseasesResult>(AppError.Conflict(
                    "DetectDiseases.MappingBatchNotApproved", "Mapping candidate batch must be approved or applied for this snapshot."));
            }

            if (!Enum.TryParse<AgriDrone.IntegrationContracts.Mapping.MappingKind>(mappingBatchRow.MappingKind, out var parsedMappingKind))
            {
                return Result.Failure<DetectMissionDiseasesResult>(AppError.Conflict(
                    "DetectDiseases.InvalidMappingKind", $"Mapping candidate batch has unrecognized MappingKind '{mappingBatchRow.MappingKind}'."));
            }

            var activeModels = await dbContext.AiModelVersions.AsNoTracking()
                .Where(model => model.IsActive && model.ModelType == AiModelType.DiseaseDetection)
                .Take(2)
                .ToListAsync(cancellationToken);
            var policy = activeModels.Count == 1 ? ReadComboPolicy(activeModels[0]) : null;
            if (policy is null)
            {
                return Result.Failure<DetectMissionDiseasesResult>(AppError.Conflict(
                    "DetectDiseases.ComboPolicyRequired",
                    "COMBO requires one active disease detection model with a versioned comboInferencePolicy."));
            }

            v3Context = new V3ComboInferenceContext(
                OrderId: orderId,
                AttemptId: mission.CaptureAttemptId!.Value,
                MappingSourceAttemptId: attemptRow.MappingSourceAttemptId,
                Approval: preApproval,
                FrozenFiles: frozenFiles,
                SnapshotId: approvedSnapshot.Id,
                MappingBatchId: mappingBatchRow.Id,
                MappingKind: parsedMappingKind,
                CaptureMap: captureMap,
                Policy: policy);

            isV3Combo = true;
        }
        else if (mission.Purpose == MissionPurpose.PlantHealth)
        {
            captureMap = await dbContext.Database.SqlQuery<CaptureMap>($"""
                SELECT bm.farm_boundary_version_id AS "BoundaryId", bm.id AS "BaseMapId"
                FROM farm.farm_base_map_versions bm
                WHERE bm.farm_id = {request.FarmId} AND bm.tenant_id = {request.TenantId}
                  AND bm.farm_boundary_version_id = {mission.FarmBoundaryVersionId}
                  AND bm.published_at IS NOT NULL
                  AND (bm.id = {mission.SourceMapVersionId} OR EXISTS
                    (SELECT 1 FROM farm.zone_map_versions zm WHERE zm.id = {mission.SourceMapVersionId} AND zm.farm_base_map_version_id = bm.id))
                """).SingleOrDefaultAsync(cancellationToken);

            if (captureMap is null)
            {
                return Result.Failure<DetectMissionDiseasesResult>(AppError.Conflict(
                    "DetectDiseases.CaptureMapRequired", "The Mission must reference its published capture base map and approved boundary."));
            }
        }
        else
        {
            return Result.Failure<DetectMissionDiseasesResult>(AppError.Conflict(
                "DetectDiseases.InvalidPurpose", "Mission purpose must be Capture (COMBO) or PlantHealth."));
        }

        // Truy vấn danh sách ảnh: V3 COMBO lấy chính xác tập ảnh đã freeze; legacy PlantHealth lấy từ mission media active
        List<MissionMedia> mediaList;
        if (isV3Combo && v3Context is not null)
        {
            var frozenAssetIds = v3Context.FrozenFiles.Select(f => f.MediaAssetId).ToHashSet();

            mediaList = await dbContext.MissionMedia
                .Include(mm => mm.Media)
                .AsNoTracking()
                .Where(mm =>
                    mm.MissionId == request.MissionId &&
                    frozenAssetIds.Contains(mm.MediaId) &&
                    mm.Media.StorageStatus == MediaStorageStatus.Active &&
                    mm.Media.DeletedAt == null)
                .OrderBy(mm => mm.CapturedAt)
                .ThenBy(mm => mm.Media.StorageKey)
                .ToListAsync(cancellationToken);

            if (mediaList.Count != v3Context.FrozenFiles.Count)
            {
                return Result.Failure<DetectMissionDiseasesResult>(AppError.Conflict(
                    "DetectDiseases.MediaMismatch", "Active mission media assets do not match frozen manifest file count."));
            }

            var frozenMap = v3Context.FrozenFiles.ToDictionary(f => f.MediaAssetId);
            foreach (var mm in mediaList)
            {
                if (string.IsNullOrWhiteSpace(mm.Media.StorageKey))
                {
                    return Result.Failure<DetectMissionDiseasesResult>(AppError.Conflict(
                        "DetectDiseases.InvalidStorageKey", $"Media asset {mm.MediaId} is missing storage key."));
                }

                if (frozenMap.TryGetValue(mm.MediaId, out var frozen) &&
                    !string.Equals(mm.Media.Checksum, frozen.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    return Result.Failure<DetectMissionDiseasesResult>(AppError.Conflict(
                        "DetectDiseases.MediaHashMismatch", $"Media asset {mm.MediaId} hash does not match frozen manifest hash."));
                }
            }
        }
        else
        {
            mediaList = await dbContext.MissionMedia
                .Include(mm => mm.Media)
                .AsNoTracking()
                .Where(mm =>
                    mm.MissionId == request.MissionId &&
                    mm.MediaRole == MissionMediaRole.RawImage &&
                    mm.Media.StorageStatus == MediaStorageStatus.Active &&
                    mm.Media.DeletedAt == null)
                .OrderBy(mm => mm.CapturedAt)
                .ThenBy(mm => mm.Media.StorageKey)
                .ToListAsync(cancellationToken);
        }

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

        HashSet<string>? expectedKeys = null;
        if (v3Context is not null)
        {
            expectedKeys = new HashSet<string>(StringComparer.Ordinal);
            if (mediaList.Count == 0 || mediaList.Any(media =>
                string.IsNullOrWhiteSpace(media.Media.StorageKey) || !expectedKeys.Add(media.Media.StorageKey)))
            {
                return Result.Failure<DetectMissionDiseasesResult>(AppError.Conflict(
                    "DetectDiseases.InvalidFrozenInputs", "Frozen COMBO inputs require unique storage keys."));
            }
        }

        var startedAt = timeProvider.GetUtcNow();
        var result = await metashapeService.DetectMissionDiseasesAsync(
            request.MissionId,
            request.TenantId,
            imageItems,
            v3Context?.Policy.Confidence ?? request.ConfidenceThreshold,
            v3Context is null ? request.PolygonGeoJson : null,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return Result.Failure<DetectMissionDiseasesResult>(
                AppError.Failure("DetectDiseases.Failed", result.ErrorMessage ?? "Failed to detect diseases."));
        }

        if (expectedKeys is not null)
        {
            var outcomes = result.ImageOutcomes;
            var successfulKeys = new HashSet<string>(StringComparer.Ordinal);
            if (outcomes is null || outcomes.Any(outcome =>
                    outcome.Status != "SUCCESS" || !successfulKeys.Add(outcome.StorageKey)) ||
                !successfulKeys.SetEquals(expectedKeys) ||
                result.Detections.Any(detection =>
                    detection.SourceStorageKey is not { } key || !successfulKeys.Contains(key)))
            {
                return Result.Failure<DetectMissionDiseasesResult>(AppError.Conflict(
                    "DetectDiseases.IncompleteImageCoverage",
                    "Every frozen COMBO input requires one successful outcome and every detection must reference a processed input."));
            }
        }

        // Lưu kết quả nhận diện bệnh vào cơ sở dữ liệu PostgreSQL
        try
        {
            var committedRun = await SaveDiseaseDetectionsToDatabaseAsync(request, result, mediaList, captureMap, mission.EndedAt ?? mission.StartedAt, startedAt, v3Context, cancellationToken);
            return Result.Success(result with { JobId = committedRun.JobId, SourceRevision = committedRun.SourceRevision });
        }
        catch (DiseaseDetectionLocationException error)
        {
            return Result.Failure<DetectMissionDiseasesResult>(AppError.Validation("DetectDiseases.InvalidLocation", error.Message));
        }
        catch (InvalidOperationException error)
        {
            return Result.Failure<DetectMissionDiseasesResult>(AppError.Conflict("DetectDiseases.ProvenanceRequired", error.Message));
        }
    }

    private async Task<(Guid? JobId, int? SourceRevision)> SaveDiseaseDetectionsToDatabaseAsync(
        DetectMissionDiseasesCommand request,
        DetectMissionDiseasesResult result,
        List<MissionMedia> mediaList,
        CaptureMap captureMap,
        DateTimeOffset? missionObservedAt,
        DateTimeOffset startedAt,
        V3ComboInferenceContext? v3Context,
        CancellationToken cancellationToken)
    {
        try
        {
            if (result.Detections.Any(d => string.IsNullOrWhiteSpace(d.Id) || string.IsNullOrWhiteSpace(d.ClassName) ||
                !double.IsFinite(d.Lat) || !double.IsFinite(d.Lon) || d.Lat is < -90 or > 90 || d.Lon is < -180 or > 180 ||
                !double.IsFinite(d.Confidence) || d.Confidence is < 0 or > 1) ||
                result.Detections.Select(d => (d.ImageName, d.Id)).Distinct().Count() != result.Detections.Count)
                throw new InvalidOperationException("Detection output needs unique source IDs, valid coordinates, classes and confidence.");
            var modelQuery = dbContext.AiModelVersions.AsNoTracking().Where(model =>
                model.ModelType == AiModelType.DiseaseDetection && model.ModelName == result.ModelName);
            var model = await (v3Context is null
                ? modelQuery.Where(model => model.IsActive)
                : modelQuery.Where(model => model.Id == v3Context.Policy.ModelVersionId))
                .SingleOrDefaultAsync(cancellationToken)
                ?? throw new InvalidOperationException("Register the exact model name returned by the detector before publishing Plant Health output.");
            if (v3Context is not null && ReadComboPolicy(model) != v3Context.Policy)
                throw new InvalidOperationException("The COMBO inference policy changed during processing.");
            var metrics = model.Metrics.RootElement;
            string RequiredMetadata(string name) => metrics.TryGetProperty(name, out var field) && field.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(field.GetString()) ? field.GetString()! :
                throw new InvalidOperationException($"The model registry is missing provenance metadata '{name}'.");
            var provenance = new PlantHealthProvenanceV4(RequiredMetadata("datasetId"), RequiredMetadata("datasetVersion"),
                RequiredMetadata("annotationRuleVersion"), model.Id, model.Version,
                "confidence:" + request.ConfidenceThreshold.ToString("R", CultureInfo.InvariantCulture), RequiredMetadata("boundaryPolicyVersion"));
            // 1. Ảnh gốc: nối theo khóa MinIO detector trả về (tên img_XXXX chỉ là tên nội bộ); service cũ không trả khóa thì thử theo tên file.
            var sources = new DetectionSourceIndex(mediaList);
            // Ảnh khoanh vùng / ảnh cắt: kiểm tra object có thật trên MinIO TRƯỚC khi mở transaction (gọi mạng, không giữ khóa mission).
            var derivedObjects = await InspectDerivedImagesAsync(request, result, sources, cancellationToken);
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            DroneMission mission;
            AiProcessingJob job;
            int version;

            if (v3Context is not null)
            {
                // ADR 0020 - Rank 2: Khóa Order FOR UPDATE để serialize với cancel/re-flight
                var orderRow = await dbContext.Database.SqlQuery<OrderLockRow>($"""
                    SELECT id AS "Id", status::text AS "Status", baseline_map_version_id AS "BaselineMapVersionId"
                    FROM survey.survey_orders
                    WHERE id = {v3Context.OrderId}
                    FOR UPDATE
                    """).SingleOrDefaultAsync(cancellationToken);

                if (orderRow is null || !string.Equals(orderRow.Status, "Processing", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Survey order {v3Context.OrderId} is no longer in Processing status at commit time.");
                }

                // ADR 0020 - Rank 7: Khóa CaptureAttempt FOR UPDATE
                var attemptRow = await dbContext.Database.SqlQuery<AttemptLockRow>($"""
                    SELECT id AS "Id", status::text AS "Status", input_approval_reference AS "InputApprovalReference",
                           processing_job_reference AS "ProcessingJobReference", mapping_source_attempt_id AS "MappingSourceAttemptId"
                    FROM survey.capture_attempts
                    WHERE id = {v3Context.AttemptId} AND order_id = {v3Context.OrderId}
                    FOR UPDATE
                    """).SingleOrDefaultAsync(cancellationToken);

                if (attemptRow is null || !string.Equals(attemptRow.Status, "PROCESSING", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException($"Capture attempt {v3Context.AttemptId} is no longer in PROCESSING status at commit time.");
                }

                if (!Guid.TryParse(attemptRow.InputApprovalReference, out var commitApprovalId) ||
                    commitApprovalId != v3Context.Approval.Id ||
                    !Guid.TryParse(attemptRow.ProcessingJobReference, out var commitJobRef) ||
                    commitJobRef != v3Context.Approval.ProcessingJobId ||
                    attemptRow.MappingSourceAttemptId != v3Context.MappingSourceAttemptId)
                {
                    throw new InvalidOperationException("Active capture attempt input approval reference or processing job changed during inference.");
                }

                // ADR 0020 - Rank 8: Khóa DroneMission FOR UPDATE
                mission = await dbContext.DroneMissions.FromSqlInterpolated($"SELECT *, xmin FROM mission.drone_missions WHERE id = {request.MissionId} FOR UPDATE")
                    .SingleAsync(cancellationToken);

                version = (await dbContext.AiProcessingJobs.Where(j => j.MissionId == request.MissionId && j.JobType == AiJobType.DiseaseDetection)
                    .Select(j => (int?)j.AttemptNumber).MaxAsync(cancellationToken) ?? 0) + 1;

                // Rank 10: Recheck capture input approval
                var approval = await dbContext.Database.SqlQuery<ApprovalRow>($"""
                    SELECT id AS "Id", input_manifest_id AS "ManifestId", manifest_fingerprint AS "ManifestHash", processing_job_id AS "ProcessingJobId"
                    FROM survey.capture_input_approvals
                    WHERE id = {v3Context.Approval.Id} AND capture_attempt_id = {v3Context.AttemptId}
                    """).SingleOrDefaultAsync(cancellationToken)
                    ?? throw new InvalidOperationException("Approved capture input record not found at commit time.");

                if (approval.ManifestId != v3Context.Approval.ManifestId ||
                    approval.ManifestHash != v3Context.Approval.ManifestHash ||
                    approval.ProcessingJobId != v3Context.Approval.ProcessingJobId)
                {
                    throw new InvalidOperationException("Approved capture input record was altered at commit time.");
                }

                // Rank 6: Recheck Snapshot
                var recheckSnapshot = await ReadVerifiedMappingSnapshotAsync(
                    v3Context.OrderId, request.TenantId, request.FarmId, request.MissionId, v3Context.AttemptId,
                    v3Context.MappingSourceAttemptId, v3Context.CaptureMap, cancellationToken)
                    ?? throw new InvalidOperationException("Approved pole count snapshot was missing or revoked at commit time.");

                if (recheckSnapshot.Id != v3Context.SnapshotId || recheckSnapshot.MappingCandidateBatchId != v3Context.MappingBatchId)
                {
                    throw new InvalidOperationException("Approved pole count snapshot candidate batch altered at commit time.");
                }

                var baselineMapId = orderRow.BaselineMapVersionId ?? v3Context.CaptureMap.BaseMapId;
                var mappingKind = v3Context.MappingKind;
                var mappingBatchId = v3Context.MappingBatchId;

                var commercialPolicyRow = await dbContext.Database.SqlQuery<CommercialPolicyRow>($"""
                    SELECT business_policy_version_id AS "PolicyVersionId"
                    FROM survey.order_commercial_snapshots
                    WHERE survey_order_id = {v3Context.OrderId}
                    """).SingleOrDefaultAsync(cancellationToken);
                var policyVersionId = commercialPolicyRow?.PolicyVersionId ?? Guid.Empty;

                Guid? supersedesHandoffId = null;
                if (version > 1)
                {
                    var prevJob = await dbContext.AiProcessingJobs.AsNoTracking()
                        .Where(j => j.MissionId == request.MissionId && j.JobType == AiJobType.DiseaseDetection && j.AttemptNumber == version - 1 && j.Status == AiJobStatus.Completed)
                        .OrderByDescending(j => j.CompletedAt)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (prevJob is not null)
                    {
                        supersedesHandoffId = CreateVersion5(
                            ComboAnalysisNamespace,
                            $"{request.TenantId:D}:{v3Context.OrderId:D}:{v3Context.AttemptId:D}:{prevJob.Id:D}:{version - 1}");
                    }
                }

                var comboProvenance = new AgriDrone.IntegrationContracts.Health.ComboAnalysisProvenanceV1(
                    RequiredMetadata("datasetId"),
                    RequiredMetadata("datasetVersion"),
                    RequiredMetadata("annotationRuleVersion"),
                    model.Id,
                    model.Version,
                    v3Context.Policy.Version,
                    RequiredMetadata("boundaryPolicyVersion"));

                // Đăng ký ảnh khoanh vùng / ảnh cắt thành media của Mission
                var registeredAt = timeProvider.GetUtcNow();
                var derivedMediaIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
                foreach (var (key, info) in derivedObjects)
                {
                    var asset = MediaAsset.Create(Guid.NewGuid(), request.TenantId, request.FarmId, info.Source.Media.Provider, key, info.StorageUri,
                        MediaType.Image, "image/jpeg", info.SizeBytes, info.Sha256, executionContext.ActorId!.Value, registeredAt);
                    dbContext.MediaAssets.Add(asset);
                    dbContext.MissionMedia.Add(MissionMedia.Create(request.MissionId, asset.Id, MissionMediaRole.Other, registeredAt, info.Source.CapturedAt));
                    derivedMediaIds[key] = asset.Id;
                }
                Guid? DerivedId(string? key) => key is not null && derivedMediaIds.TryGetValue(key, out var id) ? id : null;

                // Lấy danh sách cây hiện có của Farm trong plant.plants để so khớp
                var farmPlants = await dbContext.Database.SqlQueryRaw<PlantMatchTarget>(
                    @"SELECT id AS ""Id"", plant_code AS ""PlantCode"",
                             ST_Y(location::geometry) AS ""Latitude"",
                             ST_X(location::geometry) AS ""Longitude"",
                             row_index AS ""RowIndex"", column_index AS ""ColumnIndex"",
                             plant_profile_id AS ""PlantProfileId""
                      FROM plant.plants
                      WHERE farm_id = {0} AND lifecycle_status = 'ACTIVE'",
                    request.FarmId)
                    .ToListAsync(cancellationToken);

                var matchingToleranceM = request.MatchingToleranceMeters is > 0
                    ? request.MatchingToleranceMeters.Value
                    : 2.5;

                var findingItemsForZones = new List<(Guid FindingId, string ClassName, double Lat, double Lon, decimal Confidence, Guid? PlantProfileId, Guid? EvidenceMediaId)>();
                var preparedDetections = new List<(DiseaseDetectionItem Detection, Guid FindingId, Guid? MatchedPlantId, Guid? PlantProfileId, string? PlantCode, int? Row, int? Col, double? DistanceM, decimal? MatchConf, MissionMedia? Source, Guid? AnnotatedMediaId, Guid? CropMediaId)>();
                var infectedPlantIds = new HashSet<Guid>();

                foreach (var detection in result.Detections)
                {
                    var isDisease = detection.ClassName.StartsWith("Disease", StringComparison.OrdinalIgnoreCase) ||
                                    (!detection.ClassName.StartsWith("Healthy", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(detection.ClassName));

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

                    var source = sources.Find(detection);
                    var annotatedMediaId = DerivedId(detection.AnnotatedStorageKey);
                    var cropMediaId = DerivedId(detection.CropStorageKey);
                    var findingId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(
                        $"{request.MissionId:D}/{detection.ImageName}/{detection.Id}")).AsSpan(0, 16));

                    // F04: Chỉ đưa findings đủ điều kiện (bệnh và đạt ngưỡng tin cậy) vào đề xuất Disease Zones (loại bỏ Healthy detections)
                    if (isDisease && matchedPlantId.HasValue && nearestPlant?.PlantProfileId is not null &&
                        detection.Confidence >= v3Context.Policy.Confidence)
                    {
                        findingItemsForZones.Add((findingId, detection.ClassName, detection.Lat, detection.Lon, (decimal)detection.Confidence, nearestPlant?.PlantProfileId, source?.MediaId));
                    }
                    preparedDetections.Add((detection, findingId, matchedPlantId, nearestPlant?.PlantProfileId, nearestPlant?.PlantCode, nearestPlant?.RowIndex, nearestPlant?.ColumnIndex, nearestPlant is null ? null : minDistance, matchConf, source, annotatedMediaId, cropMediaId));
                }

                // F04: Sinh Proposal Disease Zones từ detections thật của run
                var proposedZones = BuildProposedDiseaseZones(v3Context.OrderId, v3Context.AttemptId, version, v3Context.Policy, findingItemsForZones);

                var comboSnapshot = new ComboAnalysisProcessingSnapshot(
                    SourceRevision: version,
                    CaptureAttemptId: v3Context.AttemptId,
                    FarmBoundaryVersionId: v3Context.CaptureMap.BoundaryId,
                    FarmBaseMapVersionId: v3Context.CaptureMap.BaseMapId,
                    BaselineMapVersionId: baselineMapId,
                    MappingCandidateBatchId: mappingBatchId,
                    MappingKind: mappingKind,
                    BusinessPolicyVersionId: policyVersionId,
                    InputApprovalId: approval.Id,
                    InputManifestId: approval.ManifestId,
                    InputManifestHash: approval.ManifestHash,
                    SupersedesHandoffId: supersedesHandoffId,
                    LimitationNotice: RequiredMetadata("teamLabelLimitation"),
                    Provenance: comboProvenance,
                    ApprovedJobReference: approval.ProcessingJobId,
                    DiseaseZones: proposedZones);

                job = AiProcessingJob.RecordCompletedComboRun(request.MissionId, model.Id, comboSnapshot,
                    JsonSerializer.SerializeToDocument(new { InferencePolicy = v3Context.Policy, captureMap, MediaIds = mediaList.Select(m => m.MediaId).ToArray(), mapping_candidate_batch_id = mappingBatchId, InputStorageKeys = mediaList.Select(m => m.Media.StorageKey).ToArray(), image_outcomes = result.ImageOutcomes }),
                    JsonSerializer.SerializeToDocument(result), startedAt, timeProvider.GetUtcNow());

                dbContext.AiProcessingJobs.Add(job);

                var observationsToInsert = new List<MissionPlantObservation>();
                foreach (var item in preparedDetections)
                {
                    var d = item.Detection;
                    var metadataDoc = JsonDocument.Parse(JsonSerializer.Serialize(new
                    {
                        source_finding_id = item.FindingId,
                        observed_at = item.Source?.CapturedAt ?? missionObservedAt
                            ?? throw new InvalidOperationException("Capture time or Mission flight time is required for observed-time provenance."),
                        suggested_plant_id = item.MatchedPlantId,
                        detection_id = d.Id,
                        class_id = d.ClassId,
                        class_name = d.ClassName,
                        image_name = d.ImageName,
                        crop_image_url = d.CropImageUrl,
                        annotated_image_url = d.AnnotatedImageUrl,
                        source_storage_key = item.Source?.Media.StorageKey,
                        annotated_media_id = item.AnnotatedMediaId,
                        crop_media_id = item.CropMediaId,
                        bbox = new[] { d.BboxX, d.BboxY, d.BboxW, d.BboxH },
                        plant_code = item.PlantCode,
                        row = item.Row,
                        col = item.Col,
                        plant_profile_id = item.PlantProfileId
                    }));

                    var observation = MissionPlantObservation.Create(
                        farmId: request.FarmId,
                        missionId: request.MissionId,
                        detectedLocation: new Point(d.Lon, d.Lat) { SRID = 4326 },
                        detectionConfidence: (decimal)Math.Clamp(d.Confidence, 0.0, 1.0),
                        suggestedPlantId: item.MatchedPlantId,
                        resolvedPlantId: null,
                        gpsDistanceM: GetGpsDistanceMeters(item.DistanceM, d.ImageName),
                        matchingParameters: metadataDoc,
                        evidenceMediaId: item.Source?.MediaId,
                        reviewStatus: ObservationReviewStatus.Pending,
                        aiJobId: job.Id,
                        modelVersionId: model.Id,
                        matchConfidence: item.MatchConf,
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
                        "Successfully saved {Count} disease observations and {ZoneCount} proposed zones for Mission {MissionId} ({Infected} plants infected).",
                        observationsToInsert.Count, proposedZones.Count, request.MissionId, infectedPlantIds.Count);
                }
            }
            else
            {
                // Legacy PlantHealth flow
                mission = await dbContext.DroneMissions.FromSqlInterpolated($"SELECT *, xmin FROM mission.drone_missions WHERE id = {request.MissionId} FOR UPDATE")
                    .SingleAsync(cancellationToken);
                version = (await dbContext.AiProcessingJobs.Where(j => j.MissionId == request.MissionId && j.JobType == AiJobType.DiseaseDetection)
                    .Select(j => (int?)j.AttemptNumber).MaxAsync(cancellationToken) ?? 0) + 1;

                var snapshot = new PlantHealthProcessingSnapshot(version, captureMap.BoundaryId, captureMap.BaseMapId, provenance, RequiredMetadata("teamLabelLimitation"));
                job = AiProcessingJob.RecordCompletedHealthRun(request.MissionId, model.Id, snapshot,
                    JsonSerializer.SerializeToDocument(new { request.ConfidenceThreshold, captureMap, MediaIds = mediaList.Select(m => m.MediaId).ToArray() }),
                    JsonSerializer.SerializeToDocument(result), startedAt, timeProvider.GetUtcNow());

                dbContext.AiProcessingJobs.Add(job);

                var registeredAt = timeProvider.GetUtcNow();
                var derivedMediaIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
                foreach (var (key, info) in derivedObjects)
                {
                    var asset = MediaAsset.Create(Guid.NewGuid(), request.TenantId, request.FarmId, info.Source.Media.Provider, key, info.StorageUri,
                        MediaType.Image, "image/jpeg", info.SizeBytes, info.Sha256, executionContext.ActorId!.Value, registeredAt);
                    dbContext.MediaAssets.Add(asset);
                    dbContext.MissionMedia.Add(MissionMedia.Create(request.MissionId, asset.Id, MissionMediaRole.Other, registeredAt, info.Source.CapturedAt));
                    derivedMediaIds[key] = asset.Id;
                }
                Guid? DerivedId(string? key) => key is not null && derivedMediaIds.TryGetValue(key, out var id) ? id : null;

                var farmPlants = await dbContext.Database.SqlQueryRaw<PlantMatchTarget>(
                    @"SELECT id AS ""Id"", plant_code AS ""PlantCode"",
                             ST_Y(location::geometry) AS ""Latitude"",
                             ST_X(location::geometry) AS ""Longitude"",
                             row_index AS ""RowIndex"", column_index AS ""ColumnIndex"",
                             plant_profile_id AS ""PlantProfileId""
                      FROM plant.plants
                      WHERE farm_id = {0} AND lifecycle_status = 'ACTIVE'",
                    request.FarmId)
                    .ToListAsync(cancellationToken);

                var matchingToleranceM = request.MatchingToleranceMeters is > 0
                    ? request.MatchingToleranceMeters.Value
                    : 2.5;

                var observationsToInsert = new List<MissionPlantObservation>();
                var infectedPlantIds = new HashSet<Guid>();

                foreach (var detection in result.Detections)
                {
                    var isDisease = detection.ClassName.StartsWith("Disease", StringComparison.OrdinalIgnoreCase) ||
                                    (!detection.ClassName.StartsWith("Healthy", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(detection.ClassName));

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

                    var source = sources.Find(detection);
                    var annotatedMediaId = DerivedId(detection.AnnotatedStorageKey);
                    var cropMediaId = DerivedId(detection.CropStorageKey);
                    var findingId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(
                        $"{request.MissionId:D}/{detection.ImageName}/{detection.Id}")).AsSpan(0, 16));

                    var metadataDoc = JsonDocument.Parse(JsonSerializer.Serialize(new
                    {
                        source_finding_id = findingId,
                        observed_at = source?.CapturedAt ?? missionObservedAt
                            ?? throw new InvalidOperationException("Capture time or Mission flight time is required for observed-time provenance."),
                        suggested_plant_id = matchedPlantId,
                        detection_id = detection.Id,
                        class_id = detection.ClassId,
                        class_name = detection.ClassName,
                        image_name = detection.ImageName,
                        crop_image_url = detection.CropImageUrl,
                        annotated_image_url = detection.AnnotatedImageUrl,
                        source_storage_key = source?.Media.StorageKey,
                        annotated_media_id = annotatedMediaId,
                        crop_media_id = cropMediaId,
                        bbox = new[] { detection.BboxX, detection.BboxY, detection.BboxW, detection.BboxH },
                        plant_code = nearestPlant?.PlantCode,
                        row = nearestPlant?.RowIndex,
                        col = nearestPlant?.ColumnIndex,
                        plant_profile_id = nearestPlant?.PlantProfileId
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
                        evidenceMediaId: source?.MediaId,
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

            return v3Context is null ? (null, null) : (job.Id, version);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save disease detections to database for Mission {MissionId}: {Message}",
                request.MissionId, ex.Message);
            throw;
        }
    }

    // Ảnh gốc của detection: ưu tiên khóa MinIO đầy đủ (chính xác), sau đó mới tới tên file (service cũ chưa trả khóa).
    internal sealed class DetectionSourceIndex(IEnumerable<MissionMedia> media)
    {
        private readonly Dictionary<string, MissionMedia> _byKey = media.Where(m => !string.IsNullOrEmpty(m.Media?.StorageKey))
            .GroupBy(m => m.Media.StorageKey, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        private readonly Dictionary<string, MissionMedia> _byName = media.Where(m => !string.IsNullOrEmpty(m.Media?.StorageKey))
            .GroupBy(m => Path.GetFileName(m.Media.StorageKey), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        public MissionMedia? Find(DiseaseDetectionItem detection) =>
            detection.SourceStorageKey is { } key ? _byKey.GetValueOrDefault(key)
            : string.IsNullOrEmpty(detection.ImageName) ? null : _byName.GetValueOrDefault(detection.ImageName);
    }

    internal sealed record DerivedImage(MissionMedia Source, string StorageUri, long SizeBytes, string Sha256);

    // Chỉ nhận ảnh nằm trong thư mục disease của đúng Tenant/Mission, có SHA-256 hợp lệ và ảnh gốc đã đăng ký.
    internal static string? DerivedStorageUri(string key, string? sha256, MissionMedia? source, Guid tenantId, Guid missionId)
    {
        var prefix = $"tenants/{tenantId:D}/media/{missionId:D}/disease/";
        if (source is null || !key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || key.Contains("..", StringComparison.Ordinal) ||
            sha256 is not { Length: 64 } || !sha256.All(char.IsAsciiHexDigit) ||
            !source.Media.Url.EndsWith(source.Media.StorageKey, StringComparison.Ordinal))
            return null;
        // Cùng bucket với ảnh gốc: thay phần khóa trong storage URI (minio://bucket/<khóa>).
        return source.Media.Url[..^source.Media.StorageKey.Length] + key;
    }

    private async Task<Dictionary<string, DerivedImage>> InspectDerivedImagesAsync(
        DetectMissionDiseasesCommand request, DetectMissionDiseasesResult result, DetectionSourceIndex sources, CancellationToken cancellationToken)
    {
        var found = new Dictionary<string, DerivedImage>(StringComparer.Ordinal);
        var candidates = result.Detections
            .SelectMany(d => new[] { (Key: d.AnnotatedStorageKey, Sha: d.AnnotatedSha256, Detection: d), (Key: d.CropStorageKey, Sha: d.CropSha256, Detection: d) })
            .Where(c => c.Key is not null).GroupBy(c => c.Key!, StringComparer.Ordinal).Select(g => g.First());
        foreach (var (key, sha, detection) in candidates)
        {
            var source = sources.Find(detection);
            var uri = DerivedStorageUri(key!, sha, source, request.TenantId, request.MissionId);
            var info = uri is null ? null : await objectStorage.GetInfoAsync(uri, cancellationToken);
            if (uri is null || info is not { FileSizeBytes: > 0 })
            {
                // Không chặn cả lần nhận diện: phát hiện vẫn lưu, chỉ thiếu ảnh khoanh vùng/ảnh cắt.
                logger.LogWarning("Skipping disease evidence image {StorageKey} for Mission {MissionId}: invalid reference or object not found.", key, request.MissionId);
                continue;
            }
            found[key!] = new DerivedImage(source!, uri, info.FileSizeBytes, sha!.ToLowerInvariant());
        }
        return found;
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

    private static Guid CreateVersion5(Guid namespaceId, string name)
    {
        var namespaceBytes = namespaceId.ToByteArray();
        SwapGuidByteOrder(namespaceBytes);
        var nameBytes = Encoding.UTF8.GetBytes(name);
        var input = new byte[namespaceBytes.Length + nameBytes.Length];
        Buffer.BlockCopy(namespaceBytes, 0, input, 0, namespaceBytes.Length);
        Buffer.BlockCopy(nameBytes, 0, input, namespaceBytes.Length, nameBytes.Length);

#pragma warning disable CA5350
        var hash = SHA1.HashData(input);
#pragma warning restore CA5350
        var guidBytes = hash[..16];
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
        SwapGuidByteOrder(guidBytes);
        return new Guid(guidBytes);
    }

    private static void SwapGuidByteOrder(byte[] bytes)
    {
        (bytes[0], bytes[3]) = (bytes[3], bytes[0]);
        (bytes[1], bytes[2]) = (bytes[2], bytes[1]);
        (bytes[4], bytes[5]) = (bytes[5], bytes[4]);
    }

    private static ComboInferencePolicy? ReadComboPolicy(AiModelVersion model)
    {
        var metrics = model.Metrics.RootElement;
        if (metrics.ValueKind != JsonValueKind.Object ||
            !metrics.TryGetProperty("comboInferencePolicy", out var policy) ||
            policy.ValueKind != JsonValueKind.Object ||
            !policy.TryGetProperty("version", out var version) ||
            version.ValueKind != JsonValueKind.String ||
            version.GetString() is not { Length: > 0 and <= 128 } policyVersion ||
            string.IsNullOrWhiteSpace(policyVersion) ||
            !ReadNumber("confidence", out var confidence) || confidence is < 0 or > 1 ||
            !ReadNumber("clusterRadiusMeters", out var radius) || radius <= 0 ||
            !ReadNumber("bufferMeters", out var buffer) || buffer <= 0)
        {
            return null;
        }

        return new ComboInferencePolicy(model.Id, policyVersion, confidence, radius, buffer);

        bool ReadNumber(string name, out double value)
        {
            value = 0;
            return policy.TryGetProperty(name, out var property) &&
                property.ValueKind == JsonValueKind.Number &&
                property.TryGetDouble(out value) && double.IsFinite(value);
        }
    }

    private static List<ProposedComboDiseaseZoneV1> BuildProposedDiseaseZones(
        Guid orderId,
        Guid attemptId,
        int version,
        ComboInferencePolicy policy,
        List<(Guid FindingId, string ClassName, double Lat, double Lon, decimal Confidence, Guid? PlantProfileId, Guid? EvidenceMediaId)> items)
    {
        if (items.Count == 0)
        {
            return [];
        }

        // 1. Nhóm theo loại bệnh chuẩn hóa
        var groups = items.OrderBy(item => item.FindingId)
            .GroupBy(i => i.ClassName.Trim().Replace(' ', '_').ToUpperInvariant(), StringComparer.Ordinal)
            .ToList();

        var zones = new List<ProposedComboDiseaseZoneV1>();
        int zoneIndex = 0;

        foreach (var group in groups)
        {
            var classCode = group.Key;
            var groupItems = group.GroupBy(item => item.PlantProfileId)
                .Select(matches => matches.OrderByDescending(item => item.Confidence).First())
                .ToList();
            var n = groupItems.Count;
            var visited = new bool[n];
            var clusters = new List<List<(Guid FindingId, string ClassName, double Lat, double Lon, decimal Confidence, Guid? PlantProfileId, Guid? EvidenceMediaId)>>();

            // 2. Phân cụm không gian (Spatial Clustering) bằng BFS Connected Components
            for (int i = 0; i < n; i++)
            {
                if (visited[i]) continue;

                var currentCluster = new List<(Guid FindingId, string ClassName, double Lat, double Lon, decimal Confidence, Guid? PlantProfileId, Guid? EvidenceMediaId)>();
                var queue = new Queue<int>();
                queue.Enqueue(i);
                visited[i] = true;

                while (queue.Count > 0)
                {
                    var u = queue.Dequeue();
                    currentCluster.Add(groupItems[u]);

                    for (int v = 0; v < n; v++)
                    {
                        if (visited[v]) continue;

                        var dist = CalculateHaversineMeters(groupItems[u].Lat, groupItems[u].Lon, groupItems[v].Lat, groupItems[v].Lon);
                        if (dist <= policy.ClusterRadiusMeters)
                        {
                            visited[v] = true;
                            queue.Enqueue(v);
                        }
                    }
                }

                clusters.Add(currentCluster);
            }

            // 3. Mỗi cụm bệnh độc lập tạo thành một ProposedComboDiseaseZoneV1 riêng biệt
            foreach (var cluster in clusters)
            {
                zoneIndex++;
                double minLon = double.MaxValue, maxLon = double.MinValue;
                double minLat = double.MaxValue, maxLat = double.MinValue;

                foreach (var item in cluster)
                {
                    if (item.Lon < minLon) minLon = item.Lon;
                    if (item.Lon > maxLon) maxLon = item.Lon;
                    if (item.Lat < minLat) minLat = item.Lat;
                    if (item.Lat > maxLat) maxLat = item.Lat;
                }

                const double metersPerLatitudeDegree = 111_195;
                var latBuffer = policy.BufferMeters / metersPerLatitudeDegree;
                var maxAbsoluteLatitude = Math.Max(Math.Abs(minLat), Math.Abs(maxLat));
                var lonBuffer = latBuffer / Math.Max(1e-6, Math.Cos(maxAbsoluteLatitude * Math.PI / 180));
                var pMinLon = Math.Max(-180, minLon - lonBuffer);
                var pMaxLon = Math.Min(180, maxLon + lonBuffer);
                var pMinLat = Math.Max(-90, minLat - latBuffer);
                var pMaxLat = Math.Min(90, maxLat + latBuffer);

                // Ring 5 điểm khép kín theo chiều kim đồng hồ
                var ring = new List<IReadOnlyList<double>>
                {
                    new double[] { pMinLon, pMinLat },
                    new double[] { pMaxLon, pMinLat },
                    new double[] { pMaxLon, pMaxLat },
                    new double[] { pMinLon, pMaxLat },
                    new double[] { pMinLon, pMinLat }
                };

                var polygon = new GeoJsonPolygonV3("Polygon", [ring]);

                var memberships = new List<ProposedDiseaseZoneMembershipV1>();
                var zoneMedia = new List<EvidenceMediaReferenceV4>();

                foreach (var item in cluster)
                {
                    memberships.Add(new ProposedDiseaseZoneMembershipV1(
                        FindingId: item.FindingId,
                        PlantProfileId: item.PlantProfileId,
                        MappingCandidateId: null,
                        Confidence: item.Confidence));

                    if (item.EvidenceMediaId.HasValue && zoneMedia.All(m => m.MediaAssetId != item.EvidenceMediaId.Value))
                    {
                        zoneMedia.Add(new EvidenceMediaReferenceV4(item.EvidenceMediaId.Value, "source_image"));
                    }
                }

                var proposalId = CreateVersion5(
                    ComboAnalysisNamespace,
                    $"{orderId:D}:{attemptId:D}:{version}:{zoneIndex}").ToString("D");

                zones.Add(new ProposedComboDiseaseZoneV1(
                    SourceProposalId: proposalId,
                    Geometry: polygon,
                    SuspectedClassCode: classCode,
                    MembershipVersion: 1,
                    Memberships: memberships,
                    GuidanceCandidates: [],
                    EvidenceMedia: zoneMedia));
            }
        }

        return zones;
    }
}
