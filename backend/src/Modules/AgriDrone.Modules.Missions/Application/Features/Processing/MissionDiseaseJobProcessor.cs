using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.Modules.Missions.Application.Abstractions.Media;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Application.Abstractions.Processing;
using AgriDrone.Modules.Missions.Domain.Media;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Domain.Observations;
using AgriDrone.Modules.Missions.Domain.Processing;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using AgriDrone.SharedKernel.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;

namespace AgriDrone.Modules.Missions.Application.Features.Processing;

internal interface IMissionDiseaseJobProcessor
{
    Task<Result<PreparedDiseaseDetectionContext>> PrepareContextAsync(
        Guid tenantId,
        Guid farmId,
        Guid missionId,
        CancellationToken cancellationToken);

    Task<(bool Handled, string? Error)> ExecuteJobAsync(
        MissionImageJobStore.Job job,
        Guid runId,
        CancellationToken cancellationToken);
}

internal sealed record PreparedDiseaseDetectionContext(
    DroneMission Mission,
    CaptureMapRef CaptureMap,
    V3ComboInferenceContext? V3Context,
    List<MissionMedia> MediaList,
    List<MetashapeTaskImageItem>? ImageItems,
    HashSet<string>? ExpectedKeys);

internal sealed record CaptureMapRef(Guid BoundaryId, Guid BaseMapId);

internal sealed record ComboApprovalRow(Guid Id, Guid ManifestId, string ManifestHash, Guid ProcessingJobId);

internal sealed record FrozenManifestFileRow(Guid MediaAssetId, string Sha256, long SizeBytes, string MediaRole);

internal sealed record ComboInferencePolicy(
    Guid ModelVersionId,
    string Version,
    double Confidence,
    double ClusterRadiusMeters,
    double BufferMeters);

internal sealed record V3ComboInferenceContext(
    Guid OrderId,
    Guid AttemptId,
    Guid? MappingSourceAttemptId,
    ComboApprovalRow Approval,
    List<FrozenManifestFileRow> FrozenFiles,
    Guid SnapshotId,
    Guid MappingBatchId,
    MappingKind MappingKind,
    CaptureMapRef CaptureMap,
    ComboInferencePolicy Policy);

internal sealed class MissionDiseaseJobProcessor(
    IDroneMissionRepository missionRepository,
    MissionsDbContext dbContext,
    MissionImageJobStore jobStore,
    IMetashapeService metashapeService,
    IObjectStorage objectStorage,
    TimeProvider timeProvider,
    ILogger<MissionDiseaseJobProcessor> logger) : IMissionDiseaseJobProcessor
{
    private sealed record PlantMatchTarget(
        Guid Id,
        string PlantCode,
        double Latitude,
        double Longitude,
        int? RowIndex,
        int? ColumnIndex,
        Guid? PlantProfileId);

    private sealed record OrderLockRow(Guid Id, string Status, Guid? BaselineMapVersionId);
    private sealed record CommercialPolicyRow(Guid PolicyVersionId);
    private sealed record CommercialSnapshotRow(string ServiceCode, bool ScreensDisease, Guid BusinessPolicyVersionId);
    private sealed record AttemptRow(Guid Id, string? InputApprovalReference, Guid? MappingSourceAttemptId);
    private sealed record AttemptLockRow(
        Guid Id, string Status, string? InputApprovalReference, string? ProcessingJobReference, Guid? MappingSourceAttemptId);
    private sealed record SnapshotRow(Guid Id, Guid MappingCandidateBatchId);
    private sealed record MappingBatchRow(Guid Id, string MappingKind, string Status);

    private static readonly Guid ComboAnalysisNamespace = new("e2c56a81-79b8-4e1b-93f4-3d0cf3b12345");

    private Task<SnapshotRow?> ReadVerifiedMappingSnapshotAsync(
        Guid orderId, Guid tenantId, Guid farmId, Guid missionId, Guid attemptId,
        Guid? mappingSourceAttemptId, CaptureMapRef map, CancellationToken cancellationToken) =>
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
              AND (({mappingSourceAttemptId == null} AND snapshot.mission_id = {missionId})
                 OR ({mappingSourceAttemptId != null} AND snapshot.mission_id <> {missionId}
                     AND source.status = 'ACCEPTED' AND source.mapping_source_attempt_id IS NULL
                     AND source.processing_acceptance_receipt = snapshot.id::text))
            """).SingleOrDefaultAsync(cancellationToken);

    public async Task<Result<PreparedDiseaseDetectionContext>> PrepareContextAsync(
        Guid tenantId,
        Guid farmId,
        Guid missionId,
        CancellationToken cancellationToken)
    {
        var mission = await missionRepository.GetByIdAsync(missionId, tenantId, farmId, cancellationToken);
        if (mission is null)
        {
            return Result.Failure<PreparedDiseaseDetectionContext>(MissionError.NotFound(missionId));
        }

        if (mission.SurveyOrderId is not Guid orderId)
        {
            return Result.Failure<PreparedDiseaseDetectionContext>(AppError.Conflict(
                "DetectDiseases.OrderRequired", "An Order-bound Mission is required for disease detection."));
        }

        CaptureMapRef? captureMap;
        V3ComboInferenceContext? v3Context = null;

        if (mission.Purpose == MissionPurpose.Capture)
        {
            var v3Result = await PrepareV3ComboContextAsync(
                tenantId, farmId, mission, orderId, cancellationToken);
            if (v3Result.IsFailure)
            {
                return Result.Failure<PreparedDiseaseDetectionContext>(v3Result.Error);
            }

            v3Context = v3Result.Value;
            captureMap = v3Context.CaptureMap;
        }
        else if (mission.Purpose == MissionPurpose.PlantHealth)
        {
            captureMap = await dbContext.Database.SqlQuery<CaptureMapRef>($"""
                SELECT bm.farm_boundary_version_id AS "BoundaryId", bm.id AS "BaseMapId"
                FROM farm.farm_base_map_versions bm
                WHERE bm.farm_id = {farmId} AND bm.tenant_id = {tenantId}
                  AND bm.farm_boundary_version_id = {mission.FarmBoundaryVersionId}
                  AND bm.published_at IS NOT NULL
                  AND (bm.id = {mission.SourceMapVersionId} OR EXISTS
                    (SELECT 1 FROM farm.zone_map_versions zm WHERE zm.id = {mission.SourceMapVersionId} AND zm.farm_base_map_version_id = bm.id))
                """).SingleOrDefaultAsync(cancellationToken);

            if (captureMap is null)
            {
                return Result.Failure<PreparedDiseaseDetectionContext>(AppError.Conflict(
                    "DetectDiseases.CaptureMapRequired", "The Mission must reference its published capture base map and approved boundary."));
            }
        }
        else
        {
            return Result.Failure<PreparedDiseaseDetectionContext>(AppError.Conflict(
                "DetectDiseases.InvalidPurpose", "Mission purpose must be Capture (COMBO) or PlantHealth."));
        }

        List<MissionMedia> mediaList;
        HashSet<string>? expectedKeys = null;

        if (v3Context is not null)
        {
            var frozenAssetIds = v3Context.FrozenFiles.Select(f => f.MediaAssetId).ToHashSet();

            mediaList = await dbContext.MissionMedia
                .Include(mm => mm.Media)
                .AsNoTracking()
                .Where(mm =>
                    mm.MissionId == missionId &&
                    frozenAssetIds.Contains(mm.MediaId) &&
                    mm.Media.StorageStatus == MediaStorageStatus.Active &&
                    mm.Media.DeletedAt == null)
                .OrderBy(mm => mm.CapturedAt)
                .ThenBy(mm => mm.Media.StorageKey)
                .ToListAsync(cancellationToken);

            if (mediaList.Count != v3Context.FrozenFiles.Count)
            {
                return Result.Failure<PreparedDiseaseDetectionContext>(AppError.Conflict(
                    "DetectDiseases.MediaMismatch", "Active mission media assets do not match frozen manifest file count."));
            }

            var frozenMap = v3Context.FrozenFiles.ToDictionary(f => f.MediaAssetId);
            expectedKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var mm in mediaList)
            {
                if (string.IsNullOrWhiteSpace(mm.Media.StorageKey))
                {
                    return Result.Failure<PreparedDiseaseDetectionContext>(AppError.Conflict(
                        "DetectDiseases.InvalidStorageKey", $"Media asset {mm.MediaId} is missing storage key."));
                }

                if (frozenMap.TryGetValue(mm.MediaId, out var frozen) &&
                    !string.Equals(mm.Media.Checksum, frozen.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    return Result.Failure<PreparedDiseaseDetectionContext>(AppError.Conflict(
                        "DetectDiseases.MediaHashMismatch", $"Media asset {mm.MediaId} hash does not match frozen manifest hash."));
                }

                if (!expectedKeys.Add(mm.Media.StorageKey))
                {
                    return Result.Failure<PreparedDiseaseDetectionContext>(AppError.Conflict(
                        "DetectDiseases.InvalidFrozenInputs", "Frozen COMBO inputs require unique storage keys."));
                }
            }

            if (mediaList.Count == 0)
            {
                return Result.Failure<PreparedDiseaseDetectionContext>(AppError.Conflict(
                    "DetectDiseases.InvalidFrozenInputs", "Frozen COMBO inputs require unique storage keys."));
            }
        }
        else
        {
            mediaList = await dbContext.MissionMedia
                .Include(mm => mm.Media)
                .AsNoTracking()
                .Where(mm =>
                    mm.MissionId == missionId &&
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
                    : $"tenants/{tenantId}/media/{missionId}/{mm.MediaId:N}.jpg",
                mm.Media.Url)).ToList();
        }

        return Result.Success(new PreparedDiseaseDetectionContext(
            mission, captureMap, v3Context, mediaList, imageItems, expectedKeys));
    }

    public async Task<(bool Handled, string? Error)> ExecuteJobAsync(
        MissionImageJobStore.Job job,
        Guid runId,
        CancellationToken cancellationToken)
    {
        MissionDiseaseInput input;
        try
        {
            input = job.ReadInput<MissionDiseaseInput>();
        }
        catch (Exception ex)
        {
            return (false, $"DetectDiseases.InvalidInput: {ex.Message}");
        }

        var contextResult = await PrepareContextAsync(job.TenantId, job.FarmId, job.MissionId, cancellationToken);
        if (contextResult.IsFailure)
        {
            return (false, FormatError(contextResult.Error));
        }

        var ctx = contextResult.Value;
        logger.LogInformation(
            "Executing background disease detection job {JobId} for Mission {MissionId} with {Count} images...",
            job.Id, job.MissionId, ctx.ImageItems?.Count ?? 0);

        var startedAt = timeProvider.GetUtcNow();
        var result = await metashapeService.DetectMissionDiseasesAsync(
            job.MissionId,
            job.TenantId,
            ctx.ImageItems,
            ctx.V3Context?.Policy.Confidence ?? input.ConfidenceThreshold,
            ctx.V3Context is null ? input.PolygonGeoJson : null,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return (false, $"DetectDiseases.Failed: {result.ErrorMessage ?? "Failed to detect diseases."}");
        }

        if (ctx.ExpectedKeys is not null && !HasCompleteCoverage(result, ctx.ExpectedKeys))
        {
            return (false,
                "DetectDiseases.IncompleteImageCoverage: Every frozen COMBO input requires one successful outcome and every detection must reference a processed input.");
        }

        try
        {
            await SaveAndCompleteAsync(job, runId, input, ctx, result, startedAt, cancellationToken);
            return (true, null);
        }
        catch (DiseaseDetectionLocationException ex)
        {
            return (false, $"DetectDiseases.InvalidLocation: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            return (false, $"DetectDiseases.ProvenanceRequired: {ex.Message}");
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(ex, "Background disease detection job {JobId} failed while saving Mission {MissionId}", job.Id, job.MissionId);
            return (false, $"DetectDiseases.Failed: {ex.Message}");
        }
    }

    internal static bool HasCompleteCoverage(DetectMissionDiseasesResult result, HashSet<string> expectedKeys)
    {
        var outcomes = result.ImageOutcomes;
        var successfulKeys = new HashSet<string>(StringComparer.Ordinal);
        return outcomes is not null &&
            !outcomes.Any(outcome => outcome.Status != "SUCCESS" || !successfulKeys.Add(outcome.StorageKey)) &&
            successfulKeys.SetEquals(expectedKeys) &&
            result.Detections.All(detection =>
                detection.SourceStorageKey is { } key && successfulKeys.Contains(key));
    }

    private async Task<Result<V3ComboInferenceContext>> PrepareV3ComboContextAsync(
        Guid tenantId,
        Guid farmId,
        DroneMission mission,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var commercial = await dbContext.Database.SqlQuery<CommercialSnapshotRow>($"""
            SELECT service_code AS "ServiceCode", screens_disease AS "ScreensDisease", business_policy_version_id AS "BusinessPolicyVersionId"
            FROM survey.order_commercial_snapshots
            WHERE survey_order_id = {orderId}
            """).SingleOrDefaultAsync(cancellationToken);

        if (commercial is null || commercial.ServiceCode != "COMBO" || !commercial.ScreensDisease)
        {
            return Result.Failure<V3ComboInferenceContext>(AppError.Conflict(
                "DetectDiseases.InvalidPurpose", "Only COMBO Capture missions with ScreensDisease enabled can run disease detection."));
        }

        var mapId = mission.InternallyApprovedMapVersionId ?? mission.SourceMapVersionId;
        if (mapId is null)
        {
            return Result.Failure<V3ComboInferenceContext>(AppError.Conflict(
                "DetectDiseases.CaptureMapRequired", "A referenced capture map is required."));
        }

        var captureMap = await dbContext.Database.SqlQuery<CaptureMapRef>($"""
            SELECT bm.farm_boundary_version_id AS "BoundaryId", bm.id AS "BaseMapId"
            FROM farm.farm_base_map_versions bm
            WHERE bm.farm_id = {farmId} AND bm.tenant_id = {tenantId}
              AND bm.farm_boundary_version_id = {mission.FarmBoundaryVersionId}
              AND bm.status IN ('INTERNAL_APPROVED', 'PUBLISHED')
              AND bm.id = {mapId}
            """).SingleOrDefaultAsync(cancellationToken);

        if (captureMap is null)
        {
            return Result.Failure<V3ComboInferenceContext>(AppError.Conflict(
                "DetectDiseases.CaptureMapRequired", "A valid internally approved or published capture map and boundary are required."));
        }

        var attemptRow = await dbContext.Database.SqlQuery<AttemptRow>($"""
            SELECT id AS "Id", input_approval_reference AS "InputApprovalReference",
                   mapping_source_attempt_id AS "MappingSourceAttemptId"
            FROM survey.capture_attempts
            WHERE id = {mission.CaptureAttemptId} AND order_id = {orderId}
              AND tenant_id = {tenantId} AND farm_id = {farmId} AND status = 'PROCESSING'
            """).SingleOrDefaultAsync(cancellationToken);

        if (attemptRow is null || string.IsNullOrWhiteSpace(attemptRow.InputApprovalReference))
        {
            return Result.Failure<V3ComboInferenceContext>(AppError.Conflict(
                "DetectDiseases.ApprovalRequired", "An approved capture input with valid approval reference is required."));
        }

        if (!Guid.TryParse(attemptRow.InputApprovalReference, out var approvalId))
        {
            return Result.Failure<V3ComboInferenceContext>(AppError.Conflict(
                "DetectDiseases.ApprovalInvalid", "Capture attempt input approval reference is not a valid GUID."));
        }

        var preApproval = await dbContext.Database.SqlQuery<ComboApprovalRow>($"""
            SELECT id AS "Id", input_manifest_id AS "ManifestId", manifest_fingerprint AS "ManifestHash", processing_job_id AS "ProcessingJobId"
            FROM survey.capture_input_approvals
            WHERE id = {approvalId} AND capture_attempt_id = {mission.CaptureAttemptId}
            """).SingleOrDefaultAsync(cancellationToken);

        if (preApproval is null)
        {
            return Result.Failure<V3ComboInferenceContext>(AppError.Conflict(
                "DetectDiseases.ApprovalRequired", "Approved capture input record was not found for this attempt."));
        }

        var frozenFiles = await dbContext.Database.SqlQuery<FrozenManifestFileRow>($"""
            SELECT media_asset_id AS "MediaAssetId", sha256 AS "Sha256", size_bytes AS "SizeBytes", media_role AS "MediaRole"
            FROM survey.capture_input_manifest_files
            WHERE manifest_id = {preApproval.ManifestId} AND media_role IN ('RawImage', 'RAW_IMAGE')
            """).ToListAsync(cancellationToken);

        if (frozenFiles.Count == 0)
        {
            return Result.Failure<V3ComboInferenceContext>(AppError.Conflict(
                "DetectDiseases.FrozenFilesMissing", "No frozen raw images found in approved capture manifest."));
        }

        var approvedSnapshot = await ReadVerifiedMappingSnapshotAsync(
            orderId, tenantId, farmId, mission.Id, attemptRow.Id,
            attemptRow.MappingSourceAttemptId, captureMap, cancellationToken);

        if (approvedSnapshot is null)
        {
            return Result.Failure<V3ComboInferenceContext>(AppError.Conflict(
                "DetectDiseases.PoleCountSnapshotRequired", "An approved pole count snapshot matching order, attempt, mission, base map and boundary is required."));
        }

        var mappingBatchRow = await dbContext.Database.SqlQuery<MappingBatchRow>($"""
            SELECT id AS "Id", mapping_kind AS "MappingKind", status AS "Status"
            FROM farm.mapping_candidate_batches
            WHERE id = {approvedSnapshot.MappingCandidateBatchId} AND tenant_id = {tenantId} AND farm_id = {farmId}
            """).SingleOrDefaultAsync(cancellationToken);

        if (mappingBatchRow is null || mappingBatchRow.Status is not ("Approved" or "Applied"))
        {
            return Result.Failure<V3ComboInferenceContext>(AppError.Conflict(
                "DetectDiseases.MappingBatchNotApproved", "Mapping candidate batch must be approved or applied for this snapshot."));
        }

        if (!Enum.TryParse<MappingKind>(mappingBatchRow.MappingKind, out var parsedMappingKind))
        {
            return Result.Failure<V3ComboInferenceContext>(AppError.Conflict(
                "DetectDiseases.InvalidMappingKind", $"Mapping candidate batch has unrecognized MappingKind '{mappingBatchRow.MappingKind}'."));
        }

        var activeModels = await dbContext.AiModelVersions.AsNoTracking()
            .Where(model => model.IsActive && model.ModelType == AiModelType.DiseaseDetection)
            .Take(2)
            .ToListAsync(cancellationToken);
        var policy = activeModels.Count == 1 ? ReadComboPolicy(activeModels[0]) : null;
        if (policy is null)
        {
            return Result.Failure<V3ComboInferenceContext>(AppError.Conflict(
                "DetectDiseases.ComboPolicyRequired",
                "COMBO requires one active disease detection model with a versioned comboInferencePolicy."));
        }

        return Result.Success(new V3ComboInferenceContext(
            OrderId: orderId,
            AttemptId: attemptRow.Id,
            MappingSourceAttemptId: attemptRow.MappingSourceAttemptId,
            Approval: preApproval,
            FrozenFiles: frozenFiles,
            SnapshotId: approvedSnapshot.Id,
            MappingBatchId: mappingBatchRow.Id,
            MappingKind: parsedMappingKind,
            CaptureMap: captureMap,
            Policy: policy));
    }

    private async Task SaveAndCompleteAsync(
        MissionImageJobStore.Job trackingJob,
        Guid trackingRunId,
        MissionDiseaseInput input,
        PreparedDiseaseDetectionContext ctx,
        DetectMissionDiseasesResult result,
        DateTimeOffset startedAt,
        CancellationToken cancellationToken)
    {
        if (result.Detections.Any(d => string.IsNullOrWhiteSpace(d.Id) || string.IsNullOrWhiteSpace(d.ClassName) ||
                !double.IsFinite(d.Lat) || !double.IsFinite(d.Lon) || d.Lat is < -90 or > 90 || d.Lon is < -180 or > 180 ||
                !double.IsFinite(d.Confidence) || d.Confidence is < 0 or > 1) ||
            result.Detections.Select(d => (d.ImageName, d.Id)).Distinct().Count() != result.Detections.Count)
        {
            throw new InvalidOperationException("Detection output needs unique source IDs, valid coordinates, classes and confidence.");
        }

        var v3Context = ctx.V3Context;
        var modelQuery = dbContext.AiModelVersions.AsNoTracking().Where(model =>
            model.ModelType == AiModelType.DiseaseDetection && model.ModelName == result.ModelName);
        var model = await (v3Context is null
                ? modelQuery.Where(model => model.IsActive)
                : modelQuery.Where(model => model.Id == v3Context.Policy.ModelVersionId))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("Register the exact model name returned by the detector before publishing Plant Health output.");

        if (v3Context is not null && ReadComboPolicy(model) != v3Context.Policy)
        {
            throw new InvalidOperationException("The COMBO inference policy changed during processing.");
        }

        var metrics = model.Metrics.RootElement;
        string RequiredMetadata(string name) => metrics.TryGetProperty(name, out var field) && field.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(field.GetString()) ? field.GetString()! :
            throw new InvalidOperationException($"The model registry is missing provenance metadata '{name}'.");

        var provenance = new PlantHealthProvenanceV4(
            RequiredMetadata("datasetId"),
            RequiredMetadata("datasetVersion"),
            RequiredMetadata("annotationRuleVersion"),
            model.Id,
            model.Version,
            "confidence:" + input.ConfidenceThreshold.ToString("R", CultureInfo.InvariantCulture),
            RequiredMetadata("boundaryPolicyVersion"));

        var sources = new DetectMissionDiseasesCommandHandler.DetectionSourceIndex(ctx.MediaList);
        var derivedObjects = await InspectDerivedImagesAsync(
            trackingJob.TenantId, trackingJob.MissionId, result, sources, cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        DroneMission mission;
        AiProcessingJob aiJob;
        int version;
        var completedAt = timeProvider.GetUtcNow();
        var missionObservedAt = ctx.Mission.EndedAt ?? ctx.Mission.StartedAt;

        if (v3Context is not null)
        {
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

            var attemptRow = await dbContext.Database.SqlQuery<AttemptLockRow>($"""
                SELECT id AS "Id", status::text AS "Status", input_approval_reference AS "InputApprovalReference",
                       processing_job_reference AS "ProcessingJobReference", mapping_source_attempt_id AS "MappingSourceAttemptId"
                FROM survey.capture_attempts
                WHERE id = {v3Context.AttemptId} AND order_id = {v3Context.OrderId}
                  AND tenant_id = {trackingJob.TenantId} AND farm_id = {trackingJob.FarmId}
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
                throw new InvalidOperationException("Capture input approval, processing job or mapping source changed during inference.");
            }

            mission = await dbContext.DroneMissions
                .FromSqlInterpolated($"SELECT *, xmin FROM mission.drone_missions WHERE id = {trackingJob.MissionId} FOR UPDATE")
                .SingleAsync(cancellationToken);

            version = (await dbContext.AiProcessingJobs
                .Where(j => j.MissionId == trackingJob.MissionId && j.JobType == AiJobType.DiseaseDetection)
                .Select(j => (int?)j.AttemptNumber)
                .MaxAsync(cancellationToken) ?? 0) + 1;

            var approval = await dbContext.Database.SqlQuery<ComboApprovalRow>($"""
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

            var recheckSnapshot = await ReadVerifiedMappingSnapshotAsync(
                v3Context.OrderId, trackingJob.TenantId, trackingJob.FarmId, trackingJob.MissionId,
                v3Context.AttemptId, v3Context.MappingSourceAttemptId, v3Context.CaptureMap, cancellationToken)
                ?? throw new InvalidOperationException("Approved pole count snapshot was missing or revoked at commit time.");

            if (recheckSnapshot.Id != v3Context.SnapshotId || recheckSnapshot.MappingCandidateBatchId != v3Context.MappingBatchId)
            {
                throw new InvalidOperationException("Approved pole count snapshot or candidate batch changed during inference.");
            }

            var baselineMapId = orderRow.BaselineMapVersionId ?? v3Context.CaptureMap.BaseMapId;
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
                    .Where(j => j.MissionId == trackingJob.MissionId &&
                                j.JobType == AiJobType.DiseaseDetection &&
                                j.AttemptNumber == version - 1 &&
                                j.Status == AiJobStatus.Completed)
                    .OrderByDescending(j => j.CompletedAt)
                    .FirstOrDefaultAsync(cancellationToken);

                if (prevJob is not null)
                {
                    supersedesHandoffId = CreateVersion5(
                        ComboAnalysisNamespace,
                        $"{trackingJob.TenantId:D}:{v3Context.OrderId:D}:{v3Context.AttemptId:D}:{prevJob.Id:D}:{version - 1}");
                }
            }

            var comboProvenance = new ComboAnalysisProvenanceV1(
                RequiredMetadata("datasetId"),
                RequiredMetadata("datasetVersion"),
                RequiredMetadata("annotationRuleVersion"),
                model.Id,
                model.Version,
                v3Context.Policy.Version,
                RequiredMetadata("boundaryPolicyVersion"));

            var derivedMediaIds = RegisterDerivedImages(
                trackingJob.TenantId, trackingJob.FarmId, trackingJob.MissionId, input.ActorId,
                derivedObjects, completedAt);

            var matchingToleranceM = input.MatchingToleranceMeters is > 0
                ? input.MatchingToleranceMeters.Value
                : 2.5;

            var matched = await MatchDetectionsAsync(
                trackingJob.FarmId, trackingJob.MissionId, result.Detections, sources, derivedMediaIds,
                matchingToleranceM, v3Context.Policy.Confidence, cancellationToken);

            var proposedZones = BuildProposedDiseaseZones(
                v3Context.OrderId, v3Context.AttemptId, version, v3Context.Policy, matched.ZoneFindings);

            var comboSnapshot = new ComboAnalysisProcessingSnapshot(
                SourceRevision: version,
                CaptureAttemptId: v3Context.AttemptId,
                FarmBoundaryVersionId: v3Context.CaptureMap.BoundaryId,
                FarmBaseMapVersionId: v3Context.CaptureMap.BaseMapId,
                BaselineMapVersionId: baselineMapId,
                MappingCandidateBatchId: v3Context.MappingBatchId,
                MappingKind: v3Context.MappingKind,
                BusinessPolicyVersionId: policyVersionId,
                InputApprovalId: approval.Id,
                InputManifestId: approval.ManifestId,
                InputManifestHash: approval.ManifestHash,
                SupersedesHandoffId: supersedesHandoffId,
                LimitationNotice: RequiredMetadata("teamLabelLimitation"),
                Provenance: comboProvenance,
                ApprovedJobReference: approval.ProcessingJobId,
                DiseaseZones: proposedZones);

            aiJob = AiProcessingJob.RecordCompletedComboRun(
                trackingJob.MissionId,
                model.Id,
                comboSnapshot,
                JsonSerializer.SerializeToDocument(new
                {
                    InferencePolicy = v3Context.Policy,
                    captureMap = v3Context.CaptureMap,
                    MediaIds = ctx.MediaList.Select(m => m.MediaId).ToArray(),
                    mapping_candidate_batch_id = v3Context.MappingBatchId,
                    InputStorageKeys = ctx.MediaList.Select(m => m.Media.StorageKey).ToArray(),
                    image_outcomes = result.ImageOutcomes
                }),
                JsonSerializer.SerializeToDocument(result),
                startedAt,
                completedAt);

            dbContext.AiProcessingJobs.Add(aiJob);

            var observations = BuildObservations(
                trackingJob.FarmId, trackingJob.MissionId, aiJob.Id, model,
                missionObservedAt, matched.Prepared);

            await dbContext.MissionPlantObservations.AddRangeAsync(observations, cancellationToken);

            var finalResult = result with { JobId = aiJob.Id, SourceRevision = version };
            await CommitWithCompletedTrackingJobAsync(
                trackingJob.Id, trackingRunId, finalResult, completedAt, cancellationToken);

            if (observations.Count > 0)
            {
                logger.LogInformation(
                    "Successfully saved {Count} disease observations and {ZoneCount} proposed zones for Mission {MissionId} ({Infected} plants infected).",
                    observations.Count, proposedZones.Count, trackingJob.MissionId, matched.InfectedPlantCount);
            }
        }
        else
        {
            mission = await dbContext.DroneMissions
                .FromSqlInterpolated($"SELECT *, xmin FROM mission.drone_missions WHERE id = {trackingJob.MissionId} FOR UPDATE")
                .SingleAsync(cancellationToken);

            version = (await dbContext.AiProcessingJobs
                .Where(j => j.MissionId == trackingJob.MissionId && j.JobType == AiJobType.DiseaseDetection)
                .Select(j => (int?)j.AttemptNumber)
                .MaxAsync(cancellationToken) ?? 0) + 1;

            var snapshot = new PlantHealthProcessingSnapshot(
                version, ctx.CaptureMap.BoundaryId, ctx.CaptureMap.BaseMapId,
                provenance, RequiredMetadata("teamLabelLimitation"));

            aiJob = AiProcessingJob.RecordCompletedHealthRun(
                trackingJob.MissionId,
                model.Id,
                snapshot,
                JsonSerializer.SerializeToDocument(new
                {
                    input.ConfidenceThreshold,
                    captureMap = ctx.CaptureMap,
                    MediaIds = ctx.MediaList.Select(m => m.MediaId).ToArray()
                }),
                JsonSerializer.SerializeToDocument(result),
                startedAt,
                completedAt);

            dbContext.AiProcessingJobs.Add(aiJob);

            var derivedMediaIds = RegisterDerivedImages(
                trackingJob.TenantId, trackingJob.FarmId, trackingJob.MissionId, input.ActorId,
                derivedObjects, completedAt);

            var matchingToleranceM = input.MatchingToleranceMeters is > 0
                ? input.MatchingToleranceMeters.Value
                : 2.5;

            var matched = await MatchDetectionsAsync(
                trackingJob.FarmId, trackingJob.MissionId, result.Detections, sources, derivedMediaIds,
                matchingToleranceM, comboConfidenceThreshold: null, cancellationToken);

            var observations = BuildObservations(
                trackingJob.FarmId, trackingJob.MissionId, aiJob.Id, model,
                missionObservedAt, matched.Prepared);

            await dbContext.MissionPlantObservations.AddRangeAsync(observations, cancellationToken);

            var finalResult = result with { JobId = aiJob.Id, SourceRevision = version };
            await CommitWithCompletedTrackingJobAsync(
                trackingJob.Id, trackingRunId, finalResult, completedAt, cancellationToken);

            if (observations.Count > 0)
            {
                logger.LogInformation(
                    "Successfully saved {Count} disease observations to database for Mission {MissionId} ({Infected} plants infected).",
                    observations.Count, trackingJob.MissionId, matched.InfectedPlantCount);
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task CommitWithCompletedTrackingJobAsync(
        Guid trackingJobId,
        Guid trackingRunId,
        DetectMissionDiseasesResult finalResult,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);

        var outputJson = JsonSerializer.Serialize(finalResult);
        var updated = await jobStore.FinishAsync(
            trackingJobId, trackingRunId, outputJson, error: null, completedAt, cancellationToken);
        if (updated != 1)
        {
            throw new InvalidOperationException(
                "Disease detection background job state changed before completion could be committed.");
        }
    }

    private Dictionary<string, Guid> RegisterDerivedImages(
        Guid tenantId,
        Guid farmId,
        Guid missionId,
        Guid actorId,
        Dictionary<string, DetectMissionDiseasesCommandHandler.DerivedImage> derivedObjects,
        DateTimeOffset registeredAt)
    {
        var derivedMediaIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var (key, info) in derivedObjects)
        {
            var asset = MediaAsset.Create(
                Guid.NewGuid(), tenantId, farmId, info.Source.Media.Provider,
                key, info.StorageUri, MediaType.Image, "image/jpeg",
                info.SizeBytes, info.Sha256, actorId, registeredAt);
            dbContext.MediaAssets.Add(asset);
            dbContext.MissionMedia.Add(MissionMedia.Create(
                missionId, asset.Id, MissionMediaRole.Other, registeredAt, info.Source.CapturedAt));
            derivedMediaIds[key] = asset.Id;
        }
        return derivedMediaIds;
    }

    private sealed record PreparedDetection(
        DiseaseDetectionItem Detection,
        Guid FindingId,
        Guid? MatchedPlantId,
        Guid? PlantProfileId,
        string? PlantCode,
        int? Row,
        int? Col,
        double? DistanceM,
        decimal? MatchConf,
        MissionMedia? Source,
        Guid? AnnotatedMediaId,
        Guid? CropMediaId);

    private async Task<(
        List<PreparedDetection> Prepared,
        List<(Guid FindingId, string ClassName, double Lat, double Lon, decimal Confidence, Guid? PlantProfileId, Guid? EvidenceMediaId)> ZoneFindings,
        int InfectedPlantCount)> MatchDetectionsAsync(
        Guid farmId,
        Guid missionId,
        IReadOnlyList<DiseaseDetectionItem> detections,
        DetectMissionDiseasesCommandHandler.DetectionSourceIndex sources,
        Dictionary<string, Guid> derivedMediaIds,
        double matchingToleranceM,
        double? comboConfidenceThreshold,
        CancellationToken cancellationToken)
    {
        var farmPlants = await dbContext.Database.SqlQueryRaw<PlantMatchTarget>(
            @"SELECT id AS ""Id"", plant_code AS ""PlantCode"",
                     ST_Y(location::geometry) AS ""Latitude"",
                     ST_X(location::geometry) AS ""Longitude"",
                     row_index AS ""RowIndex"", column_index AS ""ColumnIndex"",
                     plant_profile_id AS ""PlantProfileId""
              FROM plant.plants
              WHERE farm_id = {0} AND lifecycle_status = 'ACTIVE'",
            farmId)
            .ToListAsync(cancellationToken);

        Guid? DerivedId(string? key) => key is not null && derivedMediaIds.TryGetValue(key, out var id) ? id : null;

        var zoneFindings = new List<(Guid FindingId, string ClassName, double Lat, double Lon, decimal Confidence, Guid? PlantProfileId, Guid? EvidenceMediaId)>();
        var prepared = new List<PreparedDetection>();
        var infectedPlantIds = new HashSet<Guid>();

        foreach (var detection in detections)
        {
            var isDisease = detection.ClassName.StartsWith("Disease", StringComparison.OrdinalIgnoreCase) ||
                            (!detection.ClassName.StartsWith("Healthy", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(detection.ClassName));

            PlantMatchTarget? nearestPlant = null;
            var minDistance = double.MaxValue;

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
                $"{missionId:D}/{detection.ImageName}/{detection.Id}")).AsSpan(0, 16));

            if (comboConfidenceThreshold.HasValue &&
                isDisease &&
                matchedPlantId.HasValue &&
                nearestPlant?.PlantProfileId is not null &&
                detection.Confidence >= comboConfidenceThreshold.Value)
            {
                zoneFindings.Add((findingId, detection.ClassName, detection.Lat, detection.Lon, (decimal)detection.Confidence, nearestPlant.PlantProfileId, source?.MediaId));
            }

            prepared.Add(new PreparedDetection(
                detection,
                findingId,
                matchedPlantId,
                nearestPlant?.PlantProfileId,
                nearestPlant?.PlantCode,
                nearestPlant?.RowIndex,
                nearestPlant?.ColumnIndex,
                nearestPlant is null ? null : minDistance,
                matchConf,
                source,
                annotatedMediaId,
                cropMediaId));
        }

        return (prepared, zoneFindings, infectedPlantIds.Count);
    }

    private static List<MissionPlantObservation> BuildObservations(
        Guid farmId,
        Guid missionId,
        Guid aiJobId,
        AiModelVersion model,
        DateTimeOffset? missionObservedAt,
        List<PreparedDetection> prepared)
    {
        var observations = new List<MissionPlantObservation>(prepared.Count);
        foreach (var item in prepared)
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

            observations.Add(MissionPlantObservation.Create(
                farmId: farmId,
                missionId: missionId,
                detectedLocation: new Point(d.Lon, d.Lat) { SRID = 4326 },
                detectionConfidence: (decimal)Math.Clamp(d.Confidence, 0.0, 1.0),
                suggestedPlantId: item.MatchedPlantId,
                resolvedPlantId: null,
                gpsDistanceM: DetectMissionDiseasesCommandHandler.GetGpsDistanceMeters(item.DistanceM, d.ImageName),
                matchingParameters: metadataDoc,
                evidenceMediaId: item.Source?.MediaId,
                reviewStatus: ObservationReviewStatus.Pending,
                aiJobId: aiJobId,
                modelVersionId: model.Id,
                matchConfidence: item.MatchConf,
                selectedMatchStrategy: MatchStrategy.GpsOnly,
                matchingAlgorithmVersion: model.Version));
        }

        return observations;
    }

    private async Task<Dictionary<string, DetectMissionDiseasesCommandHandler.DerivedImage>> InspectDerivedImagesAsync(
        Guid tenantId,
        Guid missionId,
        DetectMissionDiseasesResult result,
        DetectMissionDiseasesCommandHandler.DetectionSourceIndex sources,
        CancellationToken cancellationToken)
    {
        var found = new Dictionary<string, DetectMissionDiseasesCommandHandler.DerivedImage>(StringComparer.Ordinal);
        var candidates = result.Detections
            .SelectMany(d => new[] { (Key: d.AnnotatedStorageKey, Sha: d.AnnotatedSha256, Detection: d), (Key: d.CropStorageKey, Sha: d.CropSha256, Detection: d) })
            .Where(c => c.Key is not null)
            .GroupBy(c => c.Key!, StringComparer.Ordinal)
            .Select(g => g.First());

        foreach (var (key, sha, detection) in candidates)
        {
            var source = sources.Find(detection);
            var uri = DetectMissionDiseasesCommandHandler.DerivedStorageUri(key!, sha, source, tenantId, missionId);
            var info = uri is null ? null : await objectStorage.GetInfoAsync(uri, cancellationToken);
            if (uri is null || info is not { FileSizeBytes: > 0 })
            {
                logger.LogWarning("Skipping disease evidence image {StorageKey} for Mission {MissionId}: invalid reference or object not found.", key, missionId);
                continue;
            }
            found[key!] = new DetectMissionDiseasesCommandHandler.DerivedImage(source!, uri, info.FileSizeBytes, sha!.ToLowerInvariant());
        }

        return found;
    }

    private static string FormatError(AppError error) => $"{error.Code}: {error.Description}";

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
