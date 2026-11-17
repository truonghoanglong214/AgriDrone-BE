using System.Text.Json;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Plants;
using AgriDrone.Modules.Farms.Application.Abstractions.Maps;
using AgriDrone.Modules.Farms.Application.Services;
using AgriDrone.Modules.Farms.Domain.Boundaries;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Domain.Maps.Staging;
using AgriDrone.Modules.Plants.Domain.Mapping;
using AgriDrone.Modules.Plants.Domain.Plants;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Messaging.Outbox;
using AgriDrone.SharedKernel.Application;
using AgriDrone.SharedKernel.Domain.Spatial;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;

namespace AgriDrone.Database.Mapping;

internal sealed class FarmBaseMapInternalApprovalOrchestrator(
    MappingPublicationDbContext dbContext,
    IBaselineMappingBatchValidator validator,
    OutboxMessageFactory outboxMessageFactory,
    IAuditWriter auditWriter,
    IHealthLevelReferenceQuery healthLevelReferenceQuery,
    TimeProvider timeProvider,
    ILogger<FarmBaseMapInternalApprovalOrchestrator> logger)
    : IFarmBaseMapInternalApprovalOrchestrator
{
    public async Task<Result<FarmBaseMapInternallyApprovedResult>> ApproveInternalAsync(
        Guid farmId,
        Guid draftId,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await dbContext.ExecuteInTransactionAsync(async ct =>
            {
                // Lock farm row FOR UPDATE
                var farm = await dbContext.Farms
                    .FromSqlInterpolated($"SELECT * FROM farm.farms WHERE id = {farmId} FOR UPDATE")
                    .SingleOrDefaultAsync(ct);

                if (farm is null)
                {
                    return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                        AppError.NotFound("Farm.NotFound", "Farm not found."));
                }

                var draft = await dbContext.FarmBaseMapVersions
                    .SingleOrDefaultAsync(m => m.Id == draftId && m.FarmId == farmId, ct);

                if (draft is null)
                {
                    return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                        AppError.NotFound("FarmBaseMapDraft.NotFound", "Farm base map draft not found."));
                }

                // Idempotency: if already approved internally, load existing snapshot and return same result
                if (draft.Status == FarmBaseMapStatus.InternalApproved)
                {
                    var existingSnapshot = await dbContext.SurveyOrderPoleCountSnapshots
                        .SingleOrDefaultAsync(s => s.FarmBaseMapVersionId == draft.Id, ct);

                    if (existingSnapshot is not null)
                    {
                        return Result.Success(new FarmBaseMapInternallyApprovedResult(
                            draft.Id,
                            draft.VersionNumber,
                            existingSnapshot.SurveyOrderId,
                            farmId,
                            existingSnapshot.VerifiedBillablePoleCount,
                            existingSnapshot.Id,
                            existingSnapshot.EvidenceFingerprint,
                            draft.ApprovedInternalAt ?? existingSnapshot.VerifiedAt));
                    }
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
                    .SingleOrDefaultAsync(b => b.FarmBaseMapVersionId == draftId, ct);

                if (batch is null)
                {
                    return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                        AppError.NotFound("MappingCandidateBatch.NotFound", "Mapping candidate batch not found."));
                }

                if (batch.Status != MappingCandidateBatchStatus.Approved)
                {
                    return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                        AppError.Validation(
                            "MappingCandidateBatch.NotApproved",
                            $"Mapping candidate batch must be in 'Approved' status to approve internally. Current status: '{batch.Status}'."));
                }

                if (batch.Candidates.Any(c => c.ReviewDecision == MappingCandidateReviewDecision.Pending))
                {
                    return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                        AppError.Validation(
                            "MappingCandidateBatch.PendingCandidates",
                            "All candidates in the batch must have an explicit reviewed decision before internal approval."));
                }

                var boundary = await dbContext.FarmBoundaries
                    .SingleOrDefaultAsync(b => b.Id == batch.FarmBoundaryVersionId, ct);

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
                    .SingleOrDefaultAsync(o => o.Id == draft.SourceSurveyOrderId, ct);

                if (order is null)
                {
                    return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                        AppError.NotFound("SurveyOrder.NotFound", "Associated survey order not found."));
                }

                if (batch.MappingKind == MappingKind.FirstBaseline)
                {
                    if (!order.RequiresBaselineMapping)
                    {
                        return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                            AppError.Conflict(
                                "SurveyOrder.NotBaselineOrder",
                                $"Survey order {order.Id} does not require baseline mapping."));
                    }

                    if (order.Status != SurveyOrderStatus.AwaitingBaselineReview)
                    {
                        return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                            AppError.Conflict(
                                "SurveyOrder.InvalidStatus",
                                $"Survey order {order.Id} is in status '{order.Status}' and cannot record initial map approval."));
                    }
                }
                else if (batch.MappingKind == MappingKind.FollowUpSnapshot)
                {
                    if (order.RequiresBaselineMapping)
                    {
                        return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                            AppError.Conflict(
                                "SurveyOrder.RequiresBaselineMapping",
                                $"Survey order {order.Id} requires baseline mapping but follow-up snapshot was provided."));
                    }

                    if (order.Status != SurveyOrderStatus.Processing)
                    {
                        return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                            AppError.Conflict(
                                "SurveyOrder.InvalidStatus",
                                $"Follow-up survey order {order.Id} must be in status 'Processing'. Current status: '{order.Status}'."));
                    }
                }

                // Check for unresolved boundary exceptions
                var hasUnresolvedBoundaryExceptions = await dbContext.BoundaryExceptions.AnyAsync(
                    e => e.FarmId == farmId &&
                         e.FarmBoundaryVersionId == boundary.Id &&
                         e.State != BoundaryExceptionState.Resolved, ct);

                if (hasUnresolvedBoundaryExceptions)
                {
                    return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                        AppError.Conflict(
                            "BoundaryException.Unresolved",
                            "Unresolved boundary exceptions exist for this boundary version. All exceptions must be resolved before internal approval."));
                }

                var now = timeProvider.GetUtcNow();
                var snapshotId = Guid.NewGuid();
                var unknownHealthLevelId = await healthLevelReferenceQuery.GetActiveUnknownIdAsync(ct);
                if (!unknownHealthLevelId.HasValue)
                {
                    return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                        AppError.Conflict(
                            "HealthLevel.UnknownMissing",
                            "The active UNKNOWN health level is not configured in the system."));
                }
                var defaultHealthLevelId = unknownHealthLevelId.Value;

                // Load active profiles & plants for farm
                var activeProfiles = await dbContext.PlantProfiles
                    .Where(p => p.FarmId == farmId && p.TenantId == order.TenantId && p.Status == PlantProfileStatus.Active)
                    .ToDictionaryAsync(p => p.Id, ct);

                var activePlantsByProfile = await dbContext.Plants
                    .Where(p => p.FarmId == farmId && p.PlantProfileId != null)
                    .ToDictionaryAsync(p => p.PlantProfileId!.Value, ct);

                var snapshotItems = new List<SurveyOrderPoleCountSnapshotItem>();
                var orderedCandidates = batch.Candidates.OrderBy(c => c.Id).ToList();

                foreach (var c in orderedCandidates)
                {
                    var effectivePos = c.CorrectedPosition ?? c.OriginalPosition;
                    // Strict boundary check: Point must be strictly inside (interior)
                    var isStrictlyInBoundary = boundary.Geometry.Covers(effectivePos) &&
                                               !boundary.Geometry.Boundary.Intersects(effectivePos);

                    Guid? resultantProfileId = null;
                    bool isAccepted;
                    string? exclusionReason = null;

                    switch (c.ReviewDecision)
                    {
                        case MappingCandidateReviewDecision.Reject:
                            isAccepted = false;
                            exclusionReason = c.ReviewReason ?? "Reviewer rejected candidate.";
                            break;

                        case MappingCandidateReviewDecision.CreateNew:
                            if (!isStrictlyInBoundary)
                            {
                                isAccepted = false;
                                exclusionReason = "Candidate position is outside or on the strict farm boundary.";
                            }
                            else
                            {
                                var newProfile = PlantProfile.Create(
                                    id: Guid.NewGuid(),
                                    tenantId: order.TenantId,
                                    farmId: farmId,
                                    physicalPoleIdentifier: c.TreeId ?? $"POLE-{c.Id.ToString()[..8].ToUpperInvariant()}",
                                    baselineLocation: effectivePos,
                                    baselineMapVersionId: draft.Id,
                                    createdAt: now);

                                var createRow = Math.Max(1, c.CorrectedRowIndex ?? c.ProposedRowIndex ?? 1);
                                var createCol = Math.Max(1, c.CorrectedColumnIndex ?? c.ProposedColumnIndex ?? 1);

                                var newPlant = Plant.CreateFromFarmMapping(
                                    id: Guid.NewGuid(),
                                    farmId: farmId,
                                    plantCode: c.TreeId ?? $"P-{c.Id.ToString()[..8].ToUpperInvariant()}",
                                    location: effectivePos,
                                    farmBaseMapVersionId: draft.Id,
                                    rowIndex: createRow,
                                    columnIndex: createCol,
                                    locationAccuracyM: null,
                                    positionConfidence: c.PositionConfidence,
                                    healthLevelId: defaultHealthLevelId,
                                    sourceMissionId: draft.SourceMissionId ?? Guid.Empty,
                                    mappedAt: now);

                                newPlant.LinkPlantProfile(newProfile.Id);

                                var changeEvent = PlantChangeEvent.CreatedWithLineage(
                                    farmId: farmId,
                                    plantProfileId: newProfile.Id,
                                    sourceSurveyOrderId: order.Id,
                                    missionId: draft.SourceMissionId,
                                    plantId: newPlant.Id,
                                    farmBaseMapVersionId: draft.Id,
                                    spatialPolicyVersionId: batch.SpatialPolicyVersionId,
                                    location: effectivePos,
                                    rowIndex: createRow,
                                    columnIndex: createCol,
                                    actorId: actorId,
                                    createdAt: now);

                                dbContext.PlantProfiles.Add(newProfile);
                                dbContext.Plants.Add(newPlant);
                                dbContext.PlantChangeEvents.Add(changeEvent);

                                activeProfiles[newProfile.Id] = newProfile;
                                activePlantsByProfile[newProfile.Id] = newPlant;

                                isAccepted = true;
                                resultantProfileId = newProfile.Id;
                            }
                            break;

                        case MappingCandidateReviewDecision.MatchExisting:
                            var matchTargetId = c.CorrectedMatchedPlantId ?? c.ProposedMatchedPlantId;
                            if (!matchTargetId.HasValue || !activeProfiles.TryGetValue(matchTargetId.Value, out var matchProfile))
                            {
                                return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                                    AppError.Validation(
                                        "MappingCandidate.TargetProfileNotFound",
                                        $"Candidate {c.Id} references target profile '{matchTargetId}' which was not found or is inactive."));
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
                            dbContext.PlantProfileObservations.Add(matchObservation);

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
                            break;

                        case MappingCandidateReviewDecision.Relocate:
                            var relocateTargetId = c.CorrectedMatchedPlantId ?? c.ProposedMatchedPlantId;
                            if (!relocateTargetId.HasValue || !activeProfiles.TryGetValue(relocateTargetId.Value, out var relocateProfile))
                            {
                                return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                                    AppError.Validation(
                                        "MappingCandidate.TargetProfileNotFound",
                                        $"Candidate {c.Id} references target profile '{relocateTargetId}' which was not found or is inactive."));
                            }

                            var oldLocation = relocateProfile.CurrentLocation;
                            var relocateDistanceM = relocateProfile.Relocate(effectivePos, draft.Id, batch.MatchingDistanceThresholdM, now);

                            activePlantsByProfile.TryGetValue(relocateProfile.Id, out var existingPlant);
                            var effectiveRelocateRow = Math.Max(1, c.CorrectedRowIndex ?? c.ProposedRowIndex ?? existingPlant?.RowIndex ?? 1);
                            var effectiveRelocateCol = Math.Max(1, c.CorrectedColumnIndex ?? c.ProposedColumnIndex ?? existingPlant?.ColumnIndex ?? 1);

                            if (existingPlant is null)
                            {
                                existingPlant = Plant.CreateFromFarmMapping(
                                    id: Guid.NewGuid(),
                                    farmId: farmId,
                                    plantCode: c.TreeId ?? $"P-{c.Id.ToString()[..8].ToUpperInvariant()}",
                                    location: effectivePos,
                                    farmBaseMapVersionId: draft.Id,
                                    rowIndex: effectiveRelocateRow,
                                    columnIndex: effectiveRelocateCol,
                                    locationAccuracyM: null,
                                    positionConfidence: c.PositionConfidence,
                                    healthLevelId: defaultHealthLevelId,
                                    sourceMissionId: draft.SourceMissionId ?? Guid.Empty,
                                    mappedAt: now);
                                existingPlant.LinkPlantProfile(relocateProfile.Id);
                                dbContext.Plants.Add(existingPlant);
                                activePlantsByProfile[relocateProfile.Id] = existingPlant;
                            }
                            else
                            {
                                existingPlant.ApplyPublishedFarmMapPosition(
                                    draft.Id,
                                    effectivePos,
                                    effectiveRelocateRow,
                                    effectiveRelocateCol,
                                    locationAccuracyM: null,
                                    positionConfidence: c.PositionConfidence,
                                    mappedAt: now);
                            }

                            var relocateChangeEvent = PlantChangeEvent.Relocated(
                                farmId: farmId,
                                plantProfileId: relocateProfile.Id,
                                sourceSurveyOrderId: order.Id,
                                missionId: draft.SourceMissionId,
                                plantId: existingPlant.Id,
                                farmBaseMapVersionId: draft.Id,
                                spatialPolicyVersionId: batch.SpatialPolicyVersionId,
                                managerDecisionId: actorId,
                                oldLocation: oldLocation,
                                newLocation: effectivePos,
                                oldRowIndex: existingPlant.RowIndex,
                                newRowIndex: effectiveRelocateRow,
                                oldColumnIndex: existingPlant.ColumnIndex,
                                newColumnIndex: effectiveRelocateCol,
                                lifecycleStatus: existingPlant.LifecycleStatus,
                                actorId: actorId,
                                createdAt: now);
                            dbContext.PlantChangeEvents.Add(relocateChangeEvent);

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
                            dbContext.PlantProfileObservations.Add(relocateObservation);

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
                            break;

                        case MappingCandidateReviewDecision.Replace:
                            var replaceTargetId = c.CorrectedMatchedPlantId ?? c.ProposedMatchedPlantId;
                            if (!replaceTargetId.HasValue || !activeProfiles.TryGetValue(replaceTargetId.Value, out var oldProfile))
                            {
                                return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                                    AppError.Validation(
                                        "MappingCandidate.TargetProfileNotFound",
                                        $"Candidate {c.Id} references target profile '{replaceTargetId}' which was not found or is inactive."));
                            }

                            var oldProfileLocation = oldProfile.CurrentLocation;
                            var newReplacementProfile = oldProfile.ReplaceWithNewProfile(
                                newProfileId: Guid.NewGuid(),
                                newPoleIdentifier: c.TreeId ?? $"POLE-{c.Id.ToString()[..8].ToUpperInvariant()}",
                                newLocation: effectivePos,
                                mapVersionId: draft.Id,
                                managerDecisionId: actorId,
                                reason: c.ReviewReason ?? "Manager approved pole replacement",
                                at: now);

                            var replaceRow = Math.Max(1, c.CorrectedRowIndex ?? c.ProposedRowIndex ?? 1);
                            var replaceCol = Math.Max(1, c.CorrectedColumnIndex ?? c.ProposedColumnIndex ?? 1);

                            var newPlantForReplaced = Plant.CreateFromFarmMapping(
                                id: Guid.NewGuid(),
                                farmId: farmId,
                                plantCode: c.TreeId ?? $"P-{c.Id.ToString()[..8].ToUpperInvariant()}",
                                location: effectivePos,
                                farmBaseMapVersionId: draft.Id,
                                rowIndex: replaceRow,
                                columnIndex: replaceCol,
                                locationAccuracyM: null,
                                positionConfidence: c.PositionConfidence,
                                healthLevelId: defaultHealthLevelId,
                                sourceMissionId: draft.SourceMissionId ?? Guid.Empty,
                                mappedAt: now);
                            newPlantForReplaced.LinkPlantProfile(newReplacementProfile.Id);

                            activePlantsByProfile.TryGetValue(oldProfile.Id, out var oldPlant);
                            if (oldPlant is not null)
                            {
                                newPlantForReplaced.LinkReplacedPlant(oldPlant.Id);
                                oldPlant.Retire(PlantLifecycleStatus.Removed, now);
                            }

                            var replaceChangeEvent = PlantChangeEvent.Replaced(
                                farmId: farmId,
                                plantProfileId: newReplacementProfile.Id,
                                predecessorPlantProfileId: oldProfile.Id,
                                sourceSurveyOrderId: order.Id,
                                missionId: draft.SourceMissionId,
                                plantId: newPlantForReplaced.Id,
                                farmBaseMapVersionId: draft.Id,
                                spatialPolicyVersionId: batch.SpatialPolicyVersionId,
                                managerDecisionId: actorId,
                                oldLocation: oldProfileLocation,
                                newLocation: effectivePos,
                                oldRowIndex: oldPlant?.RowIndex,
                                newRowIndex: replaceRow,
                                oldColumnIndex: oldPlant?.ColumnIndex,
                                newColumnIndex: replaceCol,
                                actorId: actorId,
                                createdAt: now);

                            dbContext.PlantProfiles.Add(newReplacementProfile);
                            dbContext.Plants.Add(newPlantForReplaced);
                            dbContext.PlantChangeEvents.Add(replaceChangeEvent);

                            activeProfiles[newReplacementProfile.Id] = newReplacementProfile;
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
                            break;

                        default:
                            return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                                AppError.Validation(
                                    "MappingCandidate.UnsupportedDecision",
                                    $"Unsupported review decision '{c.ReviewDecision}'."));
                    }

                    var snapshotItem = SurveyOrderPoleCountSnapshotItem.Create(
                        id: Guid.NewGuid(),
                        snapshotId: snapshotId,
                        tenantId: order.TenantId,
                        farmId: farmId,
                        mappingCandidateBatchId: batch.Id,
                        mappingCandidateId: c.Id,
                        plantProfileId: resultantProfileId,
                        effectivePosition: effectivePos,
                        reviewDecision: c.ReviewDecision.ToString(),
                        isAccepted: isAccepted,
                        isStrictlyInBoundary: isStrictlyInBoundary,
                        exclusionReason: isAccepted ? null : exclusionReason);

                    snapshotItems.Add(snapshotItem);
                    dbContext.SurveyOrderPoleCountSnapshotItems.Add(snapshotItem);
                }

                // Compute canonical evidence fingerprint before state transitions
                var fingerprint = SurveyOrderPoleCountSnapshot.ComputeEvidenceFingerprint(
                    order.Id,
                    farmId,
                    draft.SourceMissionId,
                    null,
                    draft.Id,
                    boundary.Id,
                    batch.Id,
                    batch.SpatialPolicyFingerprint,
                    snapshotItems);

                var snapshot = SurveyOrderPoleCountSnapshot.Create(
                    id: snapshotId,
                    surveyOrderId: order.Id,
                    tenantId: order.TenantId,
                    farmId: farmId,
                    missionId: draft.SourceMissionId,
                    captureAttemptId: null,
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
                        order.TenantId,
                        farmId,
                        draft.Id,
                        actorId,
                        now);

                    dbContext.FarmBaselinePointers.Add(pointer);
                }
                else
                {
                    var baselineMapId = draft.BaselineMapVersionId ??
                                        throw new InvalidOperationException("Follow-up map must have a valid BaselineMapVersionId.");

                    order.RecordFollowUpMapSnapshot(snapshot, baselineMapId, order.Version);
                }

                draft.ApproveInternal(actorId, now);
                batch.MarkApplied(now);

                // Emit FarmBaseMapInternallyApprovedV1 to outbox
                var approvedEvent = new FarmBaseMapInternallyApprovedV1(
                    ApprovalId: Guid.NewGuid(),
                    CausationId: draft.Id,
                    MappingKind: batch.MappingKind,
                    SurveyOrderId: order.Id,
                    SourceMissionId: draft.SourceMissionId ?? Guid.Empty,
                    CaptureAttemptId: draft.SourceMissionId ?? Guid.NewGuid(),
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
                    farmId.ToString("D")));

                using var newData = JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    DraftId = draft.Id,
                    draft.VersionNumber,
                    OrderId = order.Id,
                    Kind = batch.MappingKind.ToString(),
                    snapshot.VerifiedBillablePoleCount,
                    Fingerprint = fingerprint
                }));

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
                    newData: newData,
                    now);

                await dbContext.SaveChangesAsync(ct);

                logger.LogInformation(
                    "Farm base map draft {DraftId} approved internally. Order: {OrderId}, Kind: {Kind}, Verified Poles: {Count}",
                    draft.Id, order.Id, batch.MappingKind, snapshot.VerifiedBillablePoleCount);

                return Result.Success(new FarmBaseMapInternallyApprovedResult(
                    draft.Id,
                    draft.VersionNumber,
                    order.Id,
                    farmId,
                    snapshot.VerifiedBillablePoleCount,
                    snapshot.Id,
                    fingerprint,
                    now));
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to approve farm base map draft {DraftId} internally for farm {FarmId}.", draftId, farmId);
            return Result.Failure<FarmBaseMapInternallyApprovedResult>(
                AppError.Failure("FarmBaseMapInternalApproval.Failed", ex.Message));
        }
    }
}
