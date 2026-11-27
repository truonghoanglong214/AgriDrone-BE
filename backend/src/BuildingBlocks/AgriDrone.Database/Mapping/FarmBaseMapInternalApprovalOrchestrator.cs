using System.Text.Json;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.Modules.Farms.Application.Abstractions.Maps;
using AgriDrone.Modules.Farms.Application.Features.Maps.ApproveFarmBaseMapInternally;
using AgriDrone.Modules.Farms.Application.Services;
using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Domain.Maps.Staging;
using AgriDrone.Modules.Plants.Application.Abstractions.Queries;
using AgriDrone.Modules.Plants.Domain.Changes;
using AgriDrone.Modules.Plants.Domain.Mapping;
using AgriDrone.Modules.Plants.Domain.Plants;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.IntegrationContracts.Plants;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Messaging.Outbox;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Domain.Spatial;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace AgriDrone.Database.Mapping;

internal sealed class FarmBaseMapInternalApprovalOrchestrator(
    MappingPublicationDbContext dbContext,
    IBaselineMappingBatchValidator validator,
    IHealthLevelReferenceQuery healthLevelReferenceQuery,
    OutboxMessageFactory outboxMessageFactory,
    IAuditWriter auditWriter,
    TimeProvider timeProvider) : IFarmBaseMapInternalApprovalOrchestrator
{
    public async Task<Result<FarmBaseMapInternallyApprovedResult>> ApproveInternalAsync(
        Guid farmId,
        Guid draftId,
        Guid actorId,
        CancellationToken ct)
    {
        return await dbContext.ExecuteInTransactionAsync(async transactionToken =>
        {
            try
            {
                var draftContext = await dbContext.FarmBaseMapVersions
                    .AsNoTracking()
                    .Where(m => m.Id == draftId && m.FarmId == farmId)
                    .Select(m => new { m.TenantId, m.FarmId })
                    .SingleOrDefaultAsync(transactionToken);

                if (draftContext is null)
                {
                    return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                        AppError.NotFound("FarmBaseMapDraft.NotFound", "Farm base map draft not found."));
                }

                // The draft supplies the tenant scope; the tenant-scoped farm lock serializes approvals.
                var farm = await dbContext.Farms
                    .FromSqlInterpolated($"SELECT * FROM farm.farms WHERE id = {farmId} AND tenant_id = {draftContext.TenantId} FOR UPDATE")
                    .SingleOrDefaultAsync(transactionToken);

                if (farm is null)
                {
                    return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                        AppError.NotFound("Farm.NotFound", $"Farm with ID '{farmId}' not found."));
                }

                var draft = await dbContext.FarmBaseMapVersions
                    .SingleOrDefaultAsync(
                        m => m.Id == draftId &&
                             m.TenantId == draftContext.TenantId &&
                             m.FarmId == draftContext.FarmId,
                        transactionToken);

                if (draft is null)
                {
                    return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                        AppError.NotFound("FarmBaseMapDraft.NotFound", "Farm base map draft not found."));
                }

                // Idempotency: if already approved, return previous result
                if (draft.Status == FarmBaseMapStatus.InternalApproved)
                {
                    var existingSnapshot = await dbContext.SurveyOrderPoleCountSnapshots
                        .Include(s => s.Items)
                        .SingleOrDefaultAsync(
                            s => s.FarmBaseMapVersionId == draft.Id &&
                                 s.TenantId == draft.TenantId &&
                                 s.FarmId == draft.FarmId,
                            transactionToken);
                    var existingBatch = await dbContext.MappingCandidateBatches
                        .AsNoTracking()
                        .SingleOrDefaultAsync(
                            b => b.FarmBaseMapVersionId == draft.Id &&
                                 b.TenantId == draft.TenantId &&
                                 b.FarmId == draft.FarmId,
                            transactionToken);
                    if (existingSnapshot is not null &&
                        existingBatch is not null &&
                        draft.ApprovedInternalAt is { } approvedAt &&
                        draft.SourceMissionId is { } sourceMissionId &&
                        existingSnapshot.SurveyOrderId == draft.SourceSurveyOrderId &&
                        existingSnapshot.MissionId == sourceMissionId &&
                        existingSnapshot.CaptureAttemptId == existingBatch.CaptureAttemptId &&
                        existingSnapshot.FarmBoundaryVersionId == existingBatch.FarmBoundaryVersionId &&
                        existingSnapshot.MappingCandidateBatchId == existingBatch.Id &&
                        existingSnapshot.SpatialPolicyVersionId == existingBatch.SpatialPolicyVersionId)
                    {
                        var recomputedFingerprint = SurveyOrderPoleCountSnapshot.ComputeEvidenceFingerprint(
                            tenantId: existingSnapshot.TenantId,
                            surveyOrderId: existingSnapshot.SurveyOrderId,
                            farmId: existingSnapshot.FarmId,
                            missionId: existingSnapshot.MissionId,
                            captureAttemptId: existingSnapshot.CaptureAttemptId,
                            farmBaseMapVersionId: existingSnapshot.FarmBaseMapVersionId,
                            farmBoundaryVersionId: existingSnapshot.FarmBoundaryVersionId,
                            mappingCandidateBatchId: existingSnapshot.MappingCandidateBatchId,
                            spatialPolicyFingerprint: existingBatch.SpatialPolicyFingerprint,
                            items: existingSnapshot.Items);
                        if (!string.Equals(
                                recomputedFingerprint,
                                existingSnapshot.EvidenceFingerprint,
                                StringComparison.Ordinal))
                        {
                            return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                                AppError.Conflict(
                                    "FarmBaseMapDraft.IdempotencyConflict",
                                    "The approved map snapshot no longer matches its immutable approval evidence."));
                        }

                        return Result.Success(new FarmBaseMapInternallyApprovedResult(
                            FarmBaseMapVersionId: draft.Id,
                            VersionNumber: draft.VersionNumber,
                            SurveyOrderId: draft.SourceSurveyOrderId,
                            FarmId: farmId,
                            VerifiedBillablePoleCount: existingSnapshot.VerifiedBillablePoleCount,
                            PoleCountSnapshotId: existingSnapshot.Id,
                            EvidenceFingerprint: existingSnapshot.EvidenceFingerprint,
                            ApprovedAt: approvedAt));
                    }

                    return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                        AppError.Conflict(
                            "FarmBaseMapDraft.IdempotencyConflict",
                            $"Base map '{draft.Id}' is already approved internally but its batch/snapshot context is incomplete or inconsistent."));
                }

                if (draft.Status != FarmBaseMapStatus.Draft)
                {
                    return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                        AppError.Conflict(
                            "FarmBaseMapDraft.InvalidStatus",
                            $"Only draft base maps can be approved internally; current status is '{draft.Status}'."));
                }

                var batch = await dbContext.MappingCandidateBatches
                    .Include(b => b.Candidates)
                    .SingleOrDefaultAsync(
                        b => b.FarmBaseMapVersionId == draftId &&
                             b.TenantId == draft.TenantId &&
                             b.FarmId == draft.FarmId,
                        transactionToken);

                if (batch is null)
                {
                    return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                        AppError.NotFound("MappingCandidateBatch.NotFound", "Mapping candidate batch not found."));
                }

                if (batch.Status is not (MappingCandidateBatchStatus.PendingReview or
                    MappingCandidateBatchStatus.ReviewInProgress or MappingCandidateBatchStatus.Approved))
                {
                    return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                        AppError.Validation(
                            "MappingCandidateBatch.NotApproved",
                            $"Mapping candidate batch cannot be approved from status '{batch.Status}'."));
                }

                if (batch.Candidates.Any(c => c.ReviewDecision == MappingCandidateReviewDecision.Pending))
                {
                    return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                        AppError.Validation(
                            "MappingCandidateBatch.PendingCandidates",
                            "All candidates in the batch must have an explicit reviewed decision before internal approval."));
                }

                var boundary = await dbContext.FarmBoundaries
                    .SingleOrDefaultAsync(
                        b => b.Id == batch.FarmBoundaryVersionId &&
                             b.TenantId == draft.TenantId &&
                             b.FarmId == draft.FarmId,
                        transactionToken);

                if (boundary is null)
                {
                    return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                        AppError.NotFound("FarmBoundary.NotFound", "Farm boundary version not found."));
                }

                var validation = validator.Validate(batch, boundary);
                if (!validation.IsValid)
                {
                    var errorMessages = string.Join("; ", validation.Issues.Where(i => i.Severity == "Error").Select(i => i.Message));
                    return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                        AppError.Validation(
                            "MappingCandidateBatch.ValidationFailed",
                            $"Cannot approve base map due to validation errors: {errorMessages}"));
                }

                var order = await dbContext.SurveyOrders
                    .FromSqlInterpolated($"SELECT *, xmin FROM survey.survey_orders WHERE id = {draft.SourceSurveyOrderId} AND tenant_id = {draft.TenantId} AND farm_id = {draft.FarmId} FOR UPDATE")
                    .SingleOrDefaultAsync(transactionToken);

                if (order is null)
                {
                    return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                        AppError.NotFound("SurveyOrder.NotFound", "Associated survey order not found."));
                }

                var isV3 = order.Scope == ContractScope.V3;
                if (isV3)
                {
                    var attempt = await dbContext.CaptureAttempts
                        .FromSqlInterpolated($"""
                            SELECT a.* FROM survey.capture_attempts a
                            WHERE a.id = {batch.CaptureAttemptId} AND a.order_id = {order.Id}
                              AND a.tenant_id = {order.TenantId} AND a.farm_id = {order.FarmId}
                              AND EXISTS (
                                  SELECT 1 FROM mission.drone_missions m
                                  WHERE m.id = {batch.MissionId} AND m.capture_attempt_id = a.id
                                    AND m.survey_order_id = a.order_id
                                    AND m.tenant_id = a.tenant_id AND m.farm_id = a.farm_id
                              )
                            FOR SHARE OF a
                            """)
                        .SingleOrDefaultAsync(transactionToken);
                    if (attempt?.Status != CaptureAttemptStatus.Processing ||
                        order.FarmBoundaryVersionId != batch.FarmBoundaryVersionId)
                    {
                        return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                            AppError.Conflict("CaptureAttempt.InvalidMappingContext",
                                "Map approval requires the matching Processing capture attempt and pinned boundary."));
                    }
                }

                if (batch.MappingKind == MappingKind.FirstBaseline)
                {
                    if (!draft.IsBaseline || draft.VersionNumber != 1 ||
                        draft.PredecessorVersionId.HasValue || draft.BaselineMapVersionId != draft.Id)
                    {
                        return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                            AppError.Conflict(
                                "FarmBaseMapDraft.InvalidBaselineLineage",
                                "First-baseline approval requires a version-1 self-referencing baseline map."));
                    }

                    if (!order.RequiresBaselineMapping)
                    {
                        return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                            AppError.Conflict(
                                "SurveyOrder.NotBaselineOrder",
                                $"Survey order {order.Id} does not require baseline mapping."));
                    }

                    var expectedStatus = isV3
                        ? SurveyOrderStatus.Processing
                        : SurveyOrderStatus.AwaitingBaselineReview;
                    if (order.Status != expectedStatus)
                    {
                        return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                            AppError.Conflict(
                                "SurveyOrder.InvalidStatus",
                                $"Survey order {order.Id} is in status '{order.Status}' and cannot record initial map approval."));
                    }
                }
                else if (batch.MappingKind == MappingKind.FollowUpSnapshot)
                {
                    if (draft.IsBaseline || !draft.BaselineMapVersionId.HasValue ||
                        !draft.PredecessorVersionId.HasValue ||
                        draft.PredecessorVersionId != batch.ExpectedCurrentFarmBaseMapVersionId)
                    {
                        return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                            AppError.Conflict(
                                "FarmBaseMapDraft.InvalidFollowUpLineage",
                                "Follow-up approval requires the pinned baseline and exact expected predecessor."));
                    }

                    if (order.RequiresBaselineMapping)
                    {
                        return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                            AppError.Conflict(
                                "SurveyOrder.RequiresBaselineMapping",
                                $"Survey order {order.Id} requires baseline mapping but follow-up snapshot mapping was provided."));
                    }

                    if (order.Status != SurveyOrderStatus.Processing)
                    {
                        return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                            AppError.Conflict(
                                "SurveyOrder.InvalidStatus",
                                $"Follow-up survey order {order.Id} must be in status 'Processing'. Current status: '{order.Status}'."));
                    }
                }

                // Check for unresolved boundary exceptions scoped to tenant, farm, order, mission, boundary
                var hasUnresolvedBoundaryExceptions = await dbContext.BoundaryExceptions.AnyAsync(
                    e => e.TenantId == order.TenantId &&
                         e.FarmId == farmId &&
                         e.SurveyOrderId == order.Id &&
                         e.MissionId == draft.SourceMissionId &&
                         e.FarmBoundaryVersionId == boundary.Id &&
                         e.State != BoundaryExceptionState.Resolved, ct);

                if (hasUnresolvedBoundaryExceptions)
                {
                    return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                        AppError.Conflict(
                            "BoundaryException.Unresolved",
                            "Unresolved boundary exceptions exist for this mission and boundary version. All exceptions must be resolved before internal approval."));
                }

                var now = timeProvider.GetUtcNow();
                var snapshotId = Guid.NewGuid();
                var approvalId = Guid.NewGuid();
                var missionId = batch.MissionId;
                var captureAttemptId = batch.CaptureAttemptId;

                var unknownHealthLevelId = await healthLevelReferenceQuery.GetActiveUnknownIdAsync(ct);
                if (!unknownHealthLevelId.HasValue)
                {
                    return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                        AppError.Conflict(
                            "HealthLevel.UnknownMissing",
                            "The active UNKNOWN health level is not configured in the system."));
                }
                var defaultHealthLevelId = unknownHealthLevelId.Value;

                // Load active plants and profiles separately by their canonical primary keys
                var activePlantsById = await dbContext.Plants
                    .Where(p => p.FarmId == farmId && p.LifecycleStatus == PlantLifecycleStatus.Active)
                    .ToDictionaryAsync(p => p.Id, ct);

                var activeProfilesById = await dbContext.PlantProfiles
                    .Where(p => p.FarmId == farmId && p.TenantId == order.TenantId && p.Status == PlantProfileStatus.Active)
                    .ToDictionaryAsync(p => p.Id, ct);

                var activePlantsByProfile = await dbContext.Plants
                    .Where(p => p.FarmId == farmId && p.PlantProfileId != null)
                    .ToDictionaryAsync(p => p.PlantProfileId!.Value, ct);

                var snapshotItems = new List<SurveyOrderPoleCountSnapshotItem>();
                var profilesToAdd = new List<PlantProfile>();
                var plantsToAdd = new List<Plant>();
                var eventsToAdd = new List<PlantChangeEvent>();
                var observationsToAdd = new List<PlantProfileObservation>();

                var orderedCandidates = batch.Candidates.OrderBy(c => c.Id).ToList();

                var targetedPlantIds = new HashSet<Guid>();
                foreach (var candidate in orderedCandidates)
                {
                    if (candidate.ReviewDecision == MappingCandidateReviewDecision.Pending)
                    {
                        return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                            AppError.Validation("MappingCandidate.Pending", $"Candidate {candidate.Id} is still pending review."));
                    }

                    if (candidate.ReviewDecision == MappingCandidateReviewDecision.Reject)
                    {
                        continue;
                    }

                    if (candidate.ReviewDecision == MappingCandidateReviewDecision.CreateNew)
                    {
                        if (string.IsNullOrWhiteSpace(candidate.TreeId) ||
                            (candidate.CorrectedRowIndex ?? candidate.ProposedRowIndex) is not { } row || row < 1 ||
                            (candidate.CorrectedColumnIndex ?? candidate.ProposedColumnIndex) is not { } column || column < 1)
                        {
                            return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                                AppError.Validation(
                                    "MappingCandidate.InvalidCreateEvidence",
                                    $"Candidate {candidate.Id} requires TreeId and reviewed row/column >= 1."));
                        }

                        continue;
                    }

                    var targetPlantId = candidate.CorrectedMatchedPlantId ?? candidate.ProposedMatchedPlantId;
                    if (!targetPlantId.HasValue ||
                        !targetedPlantIds.Add(targetPlantId.Value) ||
                        !activePlantsById.TryGetValue(targetPlantId.Value, out var targetPlant) ||
                        !targetPlant.PlantProfileId.HasValue ||
                        !activeProfilesById.TryGetValue(targetPlant.PlantProfileId.Value, out var targetProfile))
                    {
                        return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                            AppError.Validation(
                                "MappingCandidate.InvalidTarget",
                                $"Candidate {candidate.Id} must reference one unique active target plant/profile in this farm."));
                    }

                    var effectivePosition = candidate.CorrectedPosition ?? candidate.OriginalPosition;
                    var targetDistanceM = GeodesicSpatialCalculator.CalculateDistanceMeters(
                        targetProfile.CurrentLocation.Y,
                        targetProfile.CurrentLocation.X,
                        effectivePosition.Y,
                        effectivePosition.X);
                    if (targetDistanceM > batch.MatchingDistanceThresholdM)
                    {
                        return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                            AppError.Validation(
                                "MappingCandidate.DistanceThresholdExceeded",
                                $"Candidate {candidate.Id} is {targetDistanceM:F2}m from its target, above {batch.MatchingDistanceThresholdM:F2}m."));
                    }

                    if (candidate.ReviewDecision is MappingCandidateReviewDecision.Relocate or MappingCandidateReviewDecision.Replace)
                    {
                        if ((candidate.CorrectedRowIndex ?? candidate.ProposedRowIndex) is not { } row || row < 1 ||
                            (candidate.CorrectedColumnIndex ?? candidate.ProposedColumnIndex) is not { } column || column < 1 ||
                            candidate.ReviewDecision == MappingCandidateReviewDecision.Replace && string.IsNullOrWhiteSpace(candidate.TreeId))
                        {
                            return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                                AppError.Validation(
                                    "MappingCandidate.InvalidMutationEvidence",
                                    $"Candidate {candidate.Id} requires reviewed row/column >= 1 and Replace also requires TreeId."));
                        }
                    }
                }

                foreach (var c in orderedCandidates)
                {
                    var effectivePos = c.CorrectedPosition ?? c.OriginalPosition;
                    var isStrictlyInBoundary = await dbContext.Database.SqlQuery<bool>($"""
                        SELECT ST_Within(
                            ST_SetSRID(ST_MakePoint({effectivePos.X}, {effectivePos.Y}), 4326),
                            geometry) AS "Value"
                        FROM farm.farm_boundaries
                        WHERE id = {boundary.Id}
                          AND farm_id = {farmId}
                          AND tenant_id = {order.TenantId}
                        """).SingleAsync(transactionToken);

                    if (!isStrictlyInBoundary && c.ReviewDecision != MappingCandidateReviewDecision.Reject)
                    {
                        Guid? excludedTargetPlantId = null;
                        Guid? excludedTargetProfileId = null;
                        Point? excludedPreviousPosition = null;

                        if (c.ReviewDecision is MappingCandidateReviewDecision.MatchExisting or
                            MappingCandidateReviewDecision.Relocate or MappingCandidateReviewDecision.Replace)
                        {
                            excludedTargetPlantId = c.CorrectedMatchedPlantId ?? c.ProposedMatchedPlantId;
                            if (!excludedTargetPlantId.HasValue ||
                                !activePlantsById.TryGetValue(excludedTargetPlantId.Value, out var excludedTargetPlant) ||
                                !excludedTargetPlant.PlantProfileId.HasValue ||
                                !activeProfilesById.TryGetValue(excludedTargetPlant.PlantProfileId.Value, out var excludedTargetProfile))
                            {
                                return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                                    AppError.Validation(
                                        "MappingCandidate.InvalidExcludedTarget",
                                        $"Candidate {c.Id} does not reference an active target plant/profile in this farm."));
                            }

                            excludedTargetProfileId = excludedTargetProfile.Id;
                            if (c.ReviewDecision is MappingCandidateReviewDecision.Relocate or MappingCandidateReviewDecision.Replace)
                            {
                                excludedPreviousPosition = excludedTargetProfile.CurrentLocation;
                            }
                        }

                        snapshotItems.Add(SurveyOrderPoleCountSnapshotItem.Create(
                            id: Guid.NewGuid(),
                            snapshotId: snapshotId,
                            tenantId: order.TenantId,
                            farmId: farmId,
                            mappingCandidateBatchId: batch.Id,
                            mappingCandidateId: c.Id,
                            sourceCandidateId: c.CandidateId,
                            plantProfileId: null,
                            effectivePosition: effectivePos,
                            reviewDecision: c.ReviewDecision.ToString(),
                            isAccepted: false,
                            isStrictlyInBoundary: false,
                            exclusionReason: "Candidate position is outside or on the strict farm boundary.",
                            targetPlantId: excludedTargetPlantId,
                            targetPlantProfileId: excludedTargetProfileId,
                            previousPosition: excludedPreviousPosition));
                        continue;
                    }

                    Guid? resultantProfileId = null;
                    bool isAccepted;
                    string? exclusionReason = null;

                    switch (c.ReviewDecision)
                    {
                        case MappingCandidateReviewDecision.Reject:
                            isAccepted = false;
                            exclusionReason = c.ReviewReason ?? "Reviewer rejected candidate.";

                            snapshotItems.Add(SurveyOrderPoleCountSnapshotItem.Create(
                                id: Guid.NewGuid(),
                                snapshotId: snapshotId,
                                tenantId: order.TenantId,
                                farmId: farmId,
                                mappingCandidateBatchId: batch.Id,
                                mappingCandidateId: c.Id,
                                sourceCandidateId: c.CandidateId,
                                plantProfileId: null,
                                effectivePosition: effectivePos,
                                reviewDecision: "Reject",
                                isAccepted: false,
                                isStrictlyInBoundary: isStrictlyInBoundary,
                                exclusionReason: exclusionReason));
                            break;

                        case MappingCandidateReviewDecision.CreateNew:
                            if (!isStrictlyInBoundary)
                            {
                                isAccepted = false;
                                exclusionReason = "Candidate position is outside or on the strict farm boundary.";

                                snapshotItems.Add(SurveyOrderPoleCountSnapshotItem.Create(
                                    id: Guid.NewGuid(),
                                    snapshotId: snapshotId,
                                    tenantId: order.TenantId,
                                    farmId: farmId,
                                    mappingCandidateBatchId: batch.Id,
                                    mappingCandidateId: c.Id,
                                    sourceCandidateId: c.CandidateId,
                                    plantProfileId: null,
                                    effectivePosition: effectivePos,
                                    reviewDecision: "CreateNew",
                                    isAccepted: false,
                                    isStrictlyInBoundary: isStrictlyInBoundary,
                                    exclusionReason: exclusionReason));
                            }
                            else
                            {
                                if (string.IsNullOrWhiteSpace(c.TreeId))
                                {
                                    return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                                        AppError.Validation(
                                            "MappingCandidate.MissingTreeId",
                                            $"Candidate {c.Id} is missing required TreeId."));
                                }

                                var createRow = c.CorrectedRowIndex ?? c.ProposedRowIndex;
                                var createCol = c.CorrectedColumnIndex ?? c.ProposedColumnIndex;
                                if (!createRow.HasValue || createRow.Value < 1 ||
                                    !createCol.HasValue || createCol.Value < 1)
                                {
                                    return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                                        AppError.Validation(
                                            "MappingCandidate.InvalidGridIndices",
                                            $"Candidate {c.Id} must have valid Row and Column indices >= 1 for CreateNew."));
                                }

                                var poleIdentifier = c.TreeId.Trim();
                                var newProfile = PlantProfile.Create(
                                    id: Guid.NewGuid(),
                                    tenantId: order.TenantId,
                                    farmId: farmId,
                                    physicalPoleIdentifier: poleIdentifier,
                                    baselineLocation: effectivePos,
                                    baselineMapVersionId: draft.Id,
                                    createdAt: now);

                                var newPlant = Plant.CreateFromFarmMapping(
                                    id: Guid.NewGuid(),
                                    farmId: farmId,
                                    plantCode: poleIdentifier,
                                    location: effectivePos,
                                    farmBaseMapVersionId: draft.Id,
                                    rowIndex: createRow.Value,
                                    columnIndex: createCol.Value,
                                    locationAccuracyM: null,
                                    positionConfidence: c.PositionConfidence,
                                    healthLevelId: defaultHealthLevelId,
                                    sourceMissionId: missionId,
                                    mappedAt: now);

                                newPlant.LinkPlantProfile(newProfile.Id);

                                var changeEvent = PlantChangeEvent.CreatedWithLineage(
                                    farmId: farmId,
                                    plantProfileId: newProfile.Id,
                                    sourceSurveyOrderId: order.Id,
                                    missionId: missionId,
                                    plantId: newPlant.Id,
                                    farmBaseMapVersionId: draft.Id,
                                    spatialPolicyVersionId: batch.SpatialPolicyVersionId,
                                    location: effectivePos,
                                    rowIndex: createRow.Value,
                                    columnIndex: createCol.Value,
                                    actorId: actorId,
                                    createdAt: now);

                                profilesToAdd.Add(newProfile);
                                plantsToAdd.Add(newPlant);
                                eventsToAdd.Add(changeEvent);

                                activeProfilesById[newProfile.Id] = newProfile;
                                activePlantsById[newPlant.Id] = newPlant;
                                activePlantsByProfile[newProfile.Id] = newPlant;

                                isAccepted = true;
                                resultantProfileId = newProfile.Id;

                                snapshotItems.Add(SurveyOrderPoleCountSnapshotItem.Create(
                                    id: Guid.NewGuid(),
                                    snapshotId: snapshotId,
                                    tenantId: order.TenantId,
                                    farmId: farmId,
                                    mappingCandidateBatchId: batch.Id,
                                    mappingCandidateId: c.Id,
                                    sourceCandidateId: c.CandidateId,
                                    plantProfileId: resultantProfileId,
                                    effectivePosition: effectivePos,
                                    reviewDecision: "CreateNew",
                                    isAccepted: true,
                                    isStrictlyInBoundary: true,
                                    exclusionReason: null,
                                    targetPlantId: null,
                                    targetPlantProfileId: null,
                                    previousPosition: null,
                                    effectiveRowIndex: createRow.Value,
                                    effectiveColumnIndex: createCol.Value));
                            }
                            break;

                        case MappingCandidateReviewDecision.MatchExisting:
                            var targetPlantIdForMatch = c.CorrectedMatchedPlantId ?? c.ProposedMatchedPlantId;
                            if (!targetPlantIdForMatch.HasValue || !activePlantsById.TryGetValue(targetPlantIdForMatch.Value, out var targetPlantForMatch))
                            {
                                return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                                    AppError.Validation(
                                        "MappingCandidate.TargetPlantNotFound",
                                        $"Candidate {c.Id} references target plant '{targetPlantIdForMatch}' which was not found in farm."));
                            }

                            if (!targetPlantForMatch.PlantProfileId.HasValue || !activeProfilesById.TryGetValue(targetPlantForMatch.PlantProfileId.Value, out var matchProfile))
                            {
                                return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                                    AppError.Validation(
                                        "MappingCandidate.TargetProfileNotFound",
                                        $"Plant '{targetPlantForMatch.Id}' has no active profile or profile was not found."));
                            }

                            var matchDistanceM = GeodesicSpatialCalculator.CalculateDistanceMeters(
                                matchProfile.CurrentLocation.Y,
                                matchProfile.CurrentLocation.X,
                                effectivePos.Y,
                                effectivePos.X);

                            if (matchDistanceM > batch.MatchingDistanceThresholdM)
                            {
                                return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                                    AppError.Validation(
                                        "MappingCandidate.DistanceThresholdExceeded",
                                        $"Candidate {c.Id} distance to matched profile is {matchDistanceM:F2}m, exceeding threshold {batch.MatchingDistanceThresholdM:F2}m."));
                            }

                            var matchObservation = PlantProfileObservation.Create(
                                Guid.NewGuid(),
                                matchProfile.Id,
                                order.TenantId,
                                farmId,
                                order.Id,
                                draft.Id,
                                batch.Id,
                                c.Id,
                                effectivePos,
                                matchDistanceM,
                                now,
                                actorId);
                            observationsToAdd.Add(matchObservation);

                            if (!isStrictlyInBoundary)
                            {
                                isAccepted = false;
                                exclusionReason = "Matched candidate position is outside or on the strict farm boundary.";
                            }
                            else
                            {
                                isAccepted = true;
                                resultantProfileId = matchProfile.Id;
                            }

                            snapshotItems.Add(SurveyOrderPoleCountSnapshotItem.Create(
                                id: Guid.NewGuid(),
                                snapshotId: snapshotId,
                                tenantId: order.TenantId,
                                farmId: farmId,
                                mappingCandidateBatchId: batch.Id,
                                mappingCandidateId: c.Id,
                                sourceCandidateId: c.CandidateId,
                                plantProfileId: resultantProfileId,
                                effectivePosition: effectivePos,
                                reviewDecision: "MatchExisting",
                                isAccepted: isAccepted,
                                isStrictlyInBoundary: isStrictlyInBoundary,
                                exclusionReason: exclusionReason,
                                targetPlantId: targetPlantForMatch.Id,
                                targetPlantProfileId: matchProfile.Id,
                                previousPosition: null,
                                effectiveRowIndex: targetPlantForMatch.RowIndex,
                                effectiveColumnIndex: targetPlantForMatch.ColumnIndex));
                            break;

                        case MappingCandidateReviewDecision.Relocate:
                            var targetPlantIdForRelocate = c.CorrectedMatchedPlantId ?? c.ProposedMatchedPlantId;
                            if (!targetPlantIdForRelocate.HasValue || !activePlantsById.TryGetValue(targetPlantIdForRelocate.Value, out var targetPlantForRelocate))
                            {
                                return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                                    AppError.Validation(
                                        "MappingCandidate.TargetPlantNotFound",
                                        $"Candidate {c.Id} references target plant '{targetPlantIdForRelocate}' which was not found in farm."));
                            }

                            if (!targetPlantForRelocate.PlantProfileId.HasValue || !activeProfilesById.TryGetValue(targetPlantForRelocate.PlantProfileId.Value, out var relocateProfile))
                            {
                                return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                                    AppError.Validation(
                                        "MappingCandidate.TargetProfileNotFound",
                                        $"Plant '{targetPlantForRelocate.Id}' has no active profile or profile was not found."));
                            }

                            var oldLocation = relocateProfile.CurrentLocation;
                            var relocateDistanceM = relocateProfile.Relocate(effectivePos, draft.Id, batch.MatchingDistanceThresholdM, now);

                            var effectiveRelocateRow = c.CorrectedRowIndex ?? c.ProposedRowIndex;
                            var effectiveRelocateCol = c.CorrectedColumnIndex ?? c.ProposedColumnIndex;
                            if (!effectiveRelocateRow.HasValue || effectiveRelocateRow.Value < 1 ||
                                !effectiveRelocateCol.HasValue || effectiveRelocateCol.Value < 1)
                            {
                                return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                                    AppError.Validation(
                                        "MappingCandidate.InvalidGridIndices",
                                        $"Candidate {c.Id} must have valid Row and Column indices >= 1 for Relocate."));
                            }

                            targetPlantForRelocate.ApplyPublishedFarmMapPosition(
                                draft.Id,
                                effectivePos,
                                effectiveRelocateRow.Value,
                                effectiveRelocateCol.Value,
                                locationAccuracyM: null,
                                positionConfidence: c.PositionConfidence,
                                mappedAt: now);

                            var relocateChangeEvent = PlantChangeEvent.Relocated(
                                farmId: farmId,
                                plantProfileId: relocateProfile.Id,
                                sourceSurveyOrderId: order.Id,
                                missionId: missionId,
                                plantId: targetPlantForRelocate.Id,
                                farmBaseMapVersionId: draft.Id,
                                spatialPolicyVersionId: batch.SpatialPolicyVersionId,
                                managerDecisionId: actorId,
                                oldLocation: oldLocation,
                                newLocation: effectivePos,
                                oldRowIndex: targetPlantForRelocate.RowIndex,
                                newRowIndex: effectiveRelocateRow.Value,
                                oldColumnIndex: targetPlantForRelocate.ColumnIndex,
                                newColumnIndex: effectiveRelocateCol.Value,
                                lifecycleStatus: targetPlantForRelocate.LifecycleStatus,
                                actorId: actorId,
                                createdAt: now);
                            eventsToAdd.Add(relocateChangeEvent);

                            var relocateObservation = PlantProfileObservation.Create(
                                Guid.NewGuid(),
                                relocateProfile.Id,
                                order.TenantId,
                                farmId,
                                order.Id,
                                draft.Id,
                                batch.Id,
                                c.Id,
                                effectivePos,
                                relocateDistanceM,
                                now,
                                actorId);
                            observationsToAdd.Add(relocateObservation);

                            if (!isStrictlyInBoundary)
                            {
                                isAccepted = false;
                                exclusionReason = "Relocated candidate position is outside or on the strict farm boundary.";
                            }
                            else
                            {
                                isAccepted = true;
                                resultantProfileId = relocateProfile.Id;
                            }

                            snapshotItems.Add(SurveyOrderPoleCountSnapshotItem.Create(
                                id: Guid.NewGuid(),
                                snapshotId: snapshotId,
                                tenantId: order.TenantId,
                                farmId: farmId,
                                mappingCandidateBatchId: batch.Id,
                                mappingCandidateId: c.Id,
                                sourceCandidateId: c.CandidateId,
                                plantProfileId: resultantProfileId,
                                effectivePosition: effectivePos,
                                reviewDecision: "Relocate",
                                isAccepted: isAccepted,
                                isStrictlyInBoundary: isStrictlyInBoundary,
                                exclusionReason: exclusionReason,
                                targetPlantId: targetPlantForRelocate.Id,
                                targetPlantProfileId: relocateProfile.Id,
                                previousPosition: oldLocation,
                                effectiveRowIndex: effectiveRelocateRow.Value,
                                effectiveColumnIndex: effectiveRelocateCol.Value));
                            break;

                        case MappingCandidateReviewDecision.Replace:
                            var targetPlantIdForReplace = c.CorrectedMatchedPlantId ?? c.ProposedMatchedPlantId;
                            if (!targetPlantIdForReplace.HasValue || !activePlantsById.TryGetValue(targetPlantIdForReplace.Value, out var targetPlantForReplace))
                            {
                                return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                                    AppError.Validation(
                                        "MappingCandidate.TargetPlantNotFound",
                                        $"Candidate {c.Id} references target plant '{targetPlantIdForReplace}' which was not found in farm."));
                            }

                            if (!targetPlantForReplace.PlantProfileId.HasValue || !activeProfilesById.TryGetValue(targetPlantForReplace.PlantProfileId.Value, out var oldProfile))
                            {
                                return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                                    AppError.Validation(
                                        "MappingCandidate.TargetProfileNotFound",
                                        $"Plant '{targetPlantForReplace.Id}' has no active profile or profile was not found."));
                            }

                            if (string.IsNullOrWhiteSpace(c.TreeId))
                            {
                                return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                                    AppError.Validation(
                                        "MappingCandidate.MissingTreeId",
                                        $"Candidate {c.Id} is missing required TreeId for Replace."));
                            }

                            var effectiveReplaceRow = c.CorrectedRowIndex ?? c.ProposedRowIndex;
                            var effectiveReplaceCol = c.CorrectedColumnIndex ?? c.ProposedColumnIndex;
                            if (!effectiveReplaceRow.HasValue || effectiveReplaceRow.Value < 1 ||
                                !effectiveReplaceCol.HasValue || effectiveReplaceCol.Value < 1)
                            {
                                return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                                    AppError.Validation(
                                        "MappingCandidate.InvalidGridIndices",
                                        $"Candidate {c.Id} must have valid Row and Column indices >= 1 for Replace."));
                            }

                            var oldProfileLocation = oldProfile.CurrentLocation;
                            var replacePoleCode = c.TreeId.Trim();
                            var newReplacementProfile = oldProfile.ReplaceWithNewProfile(
                                newProfileId: Guid.NewGuid(),
                                newPoleIdentifier: replacePoleCode,
                                newLocation: effectivePos,
                                mapVersionId: draft.Id,
                                managerDecisionId: actorId,
                                reason: c.ReviewReason ?? "Manager approved pole replacement",
                                at: now);

                            var newPlantForReplaced = Plant.CreateFromFarmMapping(
                                id: Guid.NewGuid(),
                                farmId: farmId,
                                plantCode: replacePoleCode,
                                location: effectivePos,
                                farmBaseMapVersionId: draft.Id,
                                rowIndex: effectiveReplaceRow.Value,
                                columnIndex: effectiveReplaceCol.Value,
                                locationAccuracyM: null,
                                positionConfidence: c.PositionConfidence,
                                healthLevelId: defaultHealthLevelId,
                                sourceMissionId: missionId,
                                mappedAt: now);
                            newPlantForReplaced.LinkPlantProfile(newReplacementProfile.Id);
                            newPlantForReplaced.LinkReplacedPlant(targetPlantForReplace.Id);
                            targetPlantForReplace.Retire(PlantLifecycleStatus.Removed, now);

                            var replaceChangeEvent = PlantChangeEvent.Replaced(
                                farmId: farmId,
                                plantProfileId: newReplacementProfile.Id,
                                predecessorPlantProfileId: oldProfile.Id,
                                sourceSurveyOrderId: order.Id,
                                missionId: missionId,
                                plantId: newPlantForReplaced.Id,
                                farmBaseMapVersionId: draft.Id,
                                spatialPolicyVersionId: batch.SpatialPolicyVersionId,
                                managerDecisionId: actorId,
                                oldLocation: oldProfileLocation,
                                newLocation: effectivePos,
                                oldRowIndex: targetPlantForReplace.RowIndex,
                                newRowIndex: effectiveReplaceRow.Value,
                                oldColumnIndex: targetPlantForReplace.ColumnIndex,
                                newColumnIndex: effectiveReplaceCol.Value,
                                actorId: actorId,
                                createdAt: now);

                            profilesToAdd.Add(newReplacementProfile);
                            plantsToAdd.Add(newPlantForReplaced);
                            eventsToAdd.Add(replaceChangeEvent);

                            activeProfilesById[newReplacementProfile.Id] = newReplacementProfile;
                            activePlantsById[newPlantForReplaced.Id] = newPlantForReplaced;
                            activePlantsByProfile[newReplacementProfile.Id] = newPlantForReplaced;

                            if (!isStrictlyInBoundary)
                            {
                                isAccepted = false;
                                exclusionReason = "Replaced candidate position is outside or on the strict farm boundary.";
                            }
                            else
                            {
                                isAccepted = true;
                                resultantProfileId = newReplacementProfile.Id;
                            }

                            snapshotItems.Add(SurveyOrderPoleCountSnapshotItem.Create(
                                id: Guid.NewGuid(),
                                snapshotId: snapshotId,
                                tenantId: order.TenantId,
                                farmId: farmId,
                                mappingCandidateBatchId: batch.Id,
                                mappingCandidateId: c.Id,
                                sourceCandidateId: c.CandidateId,
                                plantProfileId: resultantProfileId,
                                effectivePosition: effectivePos,
                                reviewDecision: "Replace",
                                isAccepted: isAccepted,
                                isStrictlyInBoundary: isStrictlyInBoundary,
                                exclusionReason: exclusionReason,
                                targetPlantId: targetPlantForReplace.Id,
                                targetPlantProfileId: oldProfile.Id,
                                previousPosition: oldProfileLocation,
                                effectiveRowIndex: effectiveReplaceRow.Value,
                                effectiveColumnIndex: effectiveReplaceCol.Value));
                            break;

                        default:
                            return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                                AppError.Validation(
                                    "MappingCandidate.UnsupportedDecision",
                                    $"Candidate {c.Id} has unsupported decision '{c.ReviewDecision}'."));
                    }
                }

                // All candidates validated successfully: now attach new entities to DbContext
                dbContext.PlantProfiles.AddRange(profilesToAdd);
                dbContext.Plants.AddRange(plantsToAdd);
                dbContext.PlantChangeEvents.AddRange(eventsToAdd);
                dbContext.PlantProfileObservations.AddRange(observationsToAdd);
                dbContext.SurveyOrderPoleCountSnapshotItems.AddRange(snapshotItems);

                // Compute deterministic canonical evidence fingerprint
                var fingerprint = SurveyOrderPoleCountSnapshot.ComputeEvidenceFingerprint(
                    tenantId: order.TenantId,
                    surveyOrderId: order.Id,
                    farmId: farmId,
                    missionId: missionId,
                    captureAttemptId: captureAttemptId,
                    farmBaseMapVersionId: draft.Id,
                    farmBoundaryVersionId: boundary.Id,
                    mappingCandidateBatchId: batch.Id,
                    spatialPolicyFingerprint: batch.SpatialPolicyFingerprint,
                    items: snapshotItems);

                var snapshot = SurveyOrderPoleCountSnapshot.Create(
                    id: snapshotId,
                    surveyOrderId: order.Id,
                    tenantId: order.TenantId,
                    farmId: farmId,
                    missionId: missionId,
                    captureAttemptId: captureAttemptId,
                    farmBaseMapVersionId: draft.Id,
                    farmBoundaryVersionId: boundary.Id,
                    mappingCandidateBatchId: batch.Id,
                    spatialPolicyVersionId: batch.SpatialPolicyVersionId,
                    evidenceFingerprint: fingerprint,
                    items: snapshotItems,
                    verifiedBy: actorId,
                    verifiedAt: now,
                    revisionNumber: 1);

                dbContext.SurveyOrderPoleCountSnapshots.Add(snapshot);

                // Update survey order based on mapping kind
                if (batch.MappingKind == MappingKind.FirstBaseline)
                {
                    order.RecordInitialMapApproval(snapshot, order.Version);

                    var pointer = FarmBaselinePointer.Create(
                        tenantId: order.TenantId,
                        farmId: farmId,
                        baselineMapVersionId: draft.Id,
                        actorId: actorId,
                        at: now);
                    dbContext.FarmBaselinePointers.Add(pointer);
                }
                else
                {
                    var baselineMapId = draft.BaselineMapVersionId ??
                                        throw new InvalidOperationException("Follow-up map must have a valid BaselineMapVersionId.");

                    order.RecordFollowUpMapSnapshot(snapshot, baselineMapId, order.Version);
                }

                if (batch.Status is MappingCandidateBatchStatus.PendingReview or MappingCandidateBatchStatus.ReviewInProgress)
                {
                    batch.Approve(actorId, "Approved during internal farm base map approval.", now);
                }

                draft.ApproveInternal(actorId, now);
                batch.MarkApplied(now);

                // Emit FarmBaseMapInternallyApprovedV1 to outbox
                var approvedEvent = new FarmBaseMapInternallyApprovedV1(
                    ApprovalId: approvalId,
                    CausationId: draft.Id,
                    MappingKind: batch.MappingKind,
                    SurveyOrderId: order.Id,
                    SourceMissionId: missionId,
                    CaptureAttemptId: captureAttemptId,
                    FarmId: farmId,
                    FarmBoundaryVersionId: boundary.Id,
                    FarmBaseMapVersionId: draft.Id,
                    BaselineMapVersionId: draft.BaselineMapVersionId ?? draft.Id,
                    MappingCandidateBatchId: batch.Id,
                    PoleCountSnapshotId: snapshot.Id,
                    EvidenceFingerprint: fingerprint,
                    VerifiedBillablePoleCount: snapshot.VerifiedBillablePoleCount,
                    ApprovedBy: actorId,
                    ApprovedAt: now);

                var envelope = IntegrationEventEnvelopeFactory.Create(
                    IntegrationEventDescriptors.FarmBaseMapInternallyApprovedV1,
                    Guid.NewGuid(),
                    draft.Id,
                    order.TenantId,
                    actorId,
                    now,
                    approvedEvent);

                dbContext.OutboxMessages.Add(outboxMessageFactory.Create(
                    envelope,
                    IntegrationEventTypes.FarmBaseMapInternallyApprovedV1,
                    draft.Id.ToString("D")));

                using var auditData = JsonSerializer.SerializeToDocument(new
                {
                    DraftId = draft.Id,
                    draft.VersionNumber,
                    SurveyOrderId = order.Id,
                    MappingKind = batch.MappingKind.ToString(),
                    snapshot.VerifiedBillablePoleCount,
                    EvidenceFingerprint = fingerprint,
                    MappingCandidateBatchId = batch.Id,
                    CaptureAttemptId = captureAttemptId
                });
                auditWriter.AddUserAction(
                    dbContext,
                    order.TenantId,
                    farmId,
                    actorId,
                    draft.Id,
                    "FarmBaseMapVersion",
                    draft.Id,
                    "APPROVE_INTERNAL",
                    oldData: null,
                    newData: auditData,
                    now);

                await dbContext.SaveChangesAsync(transactionToken);

                return Result.Success(new FarmBaseMapInternallyApprovedResult(
                    FarmBaseMapVersionId: draft.Id,
                    VersionNumber: draft.VersionNumber,
                    SurveyOrderId: order.Id,
                    FarmId: farmId,
                    VerifiedBillablePoleCount: snapshot.VerifiedBillablePoleCount,
                    PoleCountSnapshotId: snapshot.Id,
                    EvidenceFingerprint: fingerprint,
                    ApprovedAt: now));
            }
            catch (Exception)
            {
                dbContext.ChangeTracker.Clear();
                throw;
            }
        }, ct);
    }
}
