using System.Text.Json;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.Modules.Farms.Application.Abstractions.Maps;
using AgriDrone.Modules.Farms.Application.Services;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Domain.Maps.Staging;
using AgriDrone.Modules.Plants.Domain.Changes;
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

internal sealed class FarmBaseMapPublicationOrchestrator(
    MappingPublicationDbContext dbContext,
    IBaselineMappingBatchValidator validator,
    OutboxMessageFactory outboxMessageFactory,
    IAuditWriter auditWriter,
    TimeProvider timeProvider,
    ILogger<FarmBaseMapPublicationOrchestrator> logger)
    : IFarmBaseMapPublicationOrchestrator
{
    public async Task<Result<FarmBaseMapPublishedResult>> PublishAsync(
        Guid farmId,
        Guid draftId,
        Guid actorId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await dbContext.ExecuteInTransactionAsync(async ct =>
            {
                var draft = await dbContext.FarmBaseMapVersions
                    .SingleOrDefaultAsync(m => m.Id == draftId && m.FarmId == farmId, ct);

                if (draft is null)
                {
                    return Result.Failure<FarmBaseMapPublishedResult>(
                        AppError.NotFound("FarmBaseMapDraft.NotFound", "Farm base map draft not found."));
                }

                if (draft.Status != FarmBaseMapStatus.Draft)
                {
                    return Result.Failure<FarmBaseMapPublishedResult>(
                        AppError.Conflict("FarmBaseMapDraft.InvalidStatus", $"Only draft base maps can be approved internally; current status is '{draft.Status}'."));
                }

                var batch = await dbContext.MappingCandidateBatches
                    .Include(b => b.Candidates)
                    .SingleOrDefaultAsync(b => b.FarmBaseMapVersionId == draftId, ct);

                if (batch is null)
                {
                    return Result.Failure<FarmBaseMapPublishedResult>(
                        AppError.NotFound("MappingCandidateBatch.NotFound", "Mapping candidate batch not found."));
                }

                var boundary = await dbContext.FarmBoundaries
                    .SingleOrDefaultAsync(b => b.Id == batch.FarmBoundaryVersionId, ct);

                var validation = validator.Validate(batch, boundary);
                if (!validation.IsValid)
                {
                    var errorMessages = string.Join("; ", validation.Issues.Where(i => i.Severity == "Error").Select(i => i.Message));
                    return Result.Failure<FarmBaseMapPublishedResult>(
                        AppError.Validation("MappingCandidateBatch.ValidationFailed", $"Cannot approve base map due to validation errors: {errorMessages}"));
                }

                if (validation.ConfirmedPublishablePoleCount <= 0)
                {
                    return Result.Failure<FarmBaseMapPublishedResult>(
                        AppError.Validation(
                            "FarmBaseMapDraft.NoPublishablePoles",
                            "At least one candidate must have an accepted review decision (CreateNew, MatchExisting, Relocate, Replace) to approve."));
                }

                var order = await dbContext.SurveyOrders
                    .SingleOrDefaultAsync(o => o.Id == draft.SourceSurveyOrderId, ct);

                if (order is null)
                {
                    return Result.Failure<FarmBaseMapPublishedResult>(
                        AppError.NotFound("SurveyOrder.NotFound", "Associated survey order not found."));
                }

                if (order.Status != SurveyOrderStatus.AwaitingBaselineReview)
                {
                    return Result.Failure<FarmBaseMapPublishedResult>(
                        AppError.Conflict("SurveyOrder.InvalidStatus", $"Survey order {order.Id} is in status '{order.Status}' and cannot record baseline review approval."));
                }

                if (batch.Candidates.Any(c => c.ReviewDecision == MappingCandidateReviewDecision.Pending))
                {
                    return Result.Failure<FarmBaseMapPublishedResult>(
                        AppError.Conflict(
                            "MappingCandidateBatch.PendingReviewRemaining",
                            "Cannot approve farm base map while candidates remain in Pending review decision."));
                }

                var effectivePolicy = await dbContext.SpatialPolicies
                    .Where(p => p.StreamKey == "DEFAULT" &&
                                p.EffectiveFrom <= batch.ProducedAt &&
                                (p.EffectiveTo == null || p.EffectiveTo > batch.ProducedAt))
                    .OrderByDescending(p => p.VersionNumber)
                    .FirstOrDefaultAsync(ct);

                if (effectivePolicy is null)
                {
                    return Result.Failure<FarmBaseMapPublishedResult>(
                        AppError.Conflict(
                            "SpatialPolicy.NotFound",
                            "No effective spatial policy exists for the survey capture time."));
                }

                var matchingThresholdM = effectivePolicy.MatchingDistanceThresholdM;

                var now = timeProvider.GetUtcNow();
                if (order.UpdatedAt != default && now < order.UpdatedAt)
                {
                    now = order.UpdatedAt.AddMilliseconds(1);
                }

                var baselinePointer = await dbContext.FarmBaselinePointers
                    .SingleOrDefaultAsync(p => p.FarmId == farmId, ct);

                Guid baselineMapVersionId;
                if (baselinePointer is null)
                {
                    // First order creates baseline
                    draft.ConfigureLineage(
                        isBaseline: true,
                        predecessorVersionId: null,
                        baselineMapVersionId: draft.Id);

                    baselineMapVersionId = draft.Id;

                    var newPointer = FarmBaselinePointer.Create(
                        tenantId: order.TenantId,
                        farmId: farmId,
                        baselineMapVersionId: draft.Id,
                        actorId: actorId,
                        at: now);
                    dbContext.FarmBaselinePointers.Add(newPointer);
                }
                else
                {
                    // Follow-up order references existing baseline
                    var latestInternalApprovedMap = await dbContext.FarmBaseMapVersions
                        .Where(v => v.FarmId == farmId && v.Status == FarmBaseMapStatus.InternalApproved)
                        .OrderByDescending(v => v.VersionNumber)
                        .FirstOrDefaultAsync(ct);

                    draft.ConfigureLineage(
                        isBaseline: false,
                        predecessorVersionId: latestInternalApprovedMap?.Id ?? baselinePointer.CurrentBaselineMapVersionId,
                        baselineMapVersionId: baselinePointer.CurrentBaselineMapVersionId);

                    baselineMapVersionId = baselinePointer.CurrentBaselineMapVersionId;
                }

                var defaultHealth = await dbContext.HealthLevels
                    .Where(h => h.IsActive && h.Code == "UNKNOWN")
                    .FirstOrDefaultAsync(ct)
                    ?? await dbContext.HealthLevels
                        .Where(h => h.IsActive)
                        .OrderBy(h => h.Rank)
                        .FirstOrDefaultAsync(ct);

                var healthId = defaultHealth?.Id
                    ?? Guid.Parse("11111111-1111-4111-8111-111111111101");

                var publishableCandidates = batch.Candidates
                    .Where(c => c.ReviewDecision is MappingCandidateReviewDecision.CreateNew
                                                 or MappingCandidateReviewDecision.MatchExisting
                                                 or MappingCandidateReviewDecision.Relocate
                                                 or MappingCandidateReviewDecision.Replace)
                    .ToList();

                var plantMappings = new List<PublishedPlantMappingV3>();
                var candidateToProfileMap = new Dictionary<Guid, Guid>();

                foreach (var c in publishableCandidates)
                {
                    var pos = c.CorrectedPosition ?? c.OriginalPosition;
                    var row = c.CorrectedRowIndex ?? c.ProposedRowIndex ?? 1;
                    var col = c.CorrectedColumnIndex ?? c.ProposedColumnIndex ?? 1;

                    if (c.ReviewDecision == MappingCandidateReviewDecision.MatchExisting)
                    {
                        var targetPlantId = c.CorrectedMatchedPlantId ?? c.ProposedMatchedPlantId;
                        if (!targetPlantId.HasValue)
                        {
                            return Result.Failure<FarmBaseMapPublishedResult>(
                                AppError.Validation(
                                    "MappingCandidate.MissingMatchedPlantId",
                                    $"Candidate {c.CandidateId} has MatchExisting decision but lacks target plant ID."));
                        }

                        var existingPlant = await dbContext.Plants
                            .SingleOrDefaultAsync(p => p.Id == targetPlantId.Value, ct);

                        if (existingPlant is null)
                        {
                            return Result.Failure<FarmBaseMapPublishedResult>(
                                AppError.NotFound(
                                    "Plant.NotFound",
                                    $"Target plant {targetPlantId.Value} for candidate {c.CandidateId} was not found."));
                        }

                        var profile = await dbContext.PlantProfiles
                            .FirstOrDefaultAsync(p => p.FarmId == farmId &&
                                (p.Id == existingPlant.PlantProfileId || p.PhysicalPoleIdentifier == existingPlant.PlantCode) &&
                                p.Status == PlantProfileStatus.Active, ct);

                        if (profile is null)
                        {
                            var newProfileId = Guid.NewGuid();
                            profile = PlantProfile.Create(
                                id: newProfileId,
                                tenantId: order.TenantId,
                                farmId: farmId,
                                physicalPoleIdentifier: existingPlant.PlantCode,
                                baselineLocation: existingPlant.Location ?? pos,
                                baselineMapVersionId: draft.Id,
                                createdAt: now);
                            dbContext.PlantProfiles.Add(profile);
                        }

                        existingPlant.LinkPlantProfile(profile.Id);

                        existingPlant.ApplyPublishedFarmMapPosition(
                            farmBaseMapVersionId: draft.Id,
                            location: existingPlant.Location ?? pos,
                            rowIndex: existingPlant.RowIndex ?? row,
                            columnIndex: existingPlant.ColumnIndex ?? col,
                            locationAccuracyM: existingPlant.LocationAccuracyM,
                            positionConfidence: c.PositionConfidence,
                            mappedAt: now);

                        var targetLocation = existingPlant.Location ?? pos;

                        var observation = PlantProfileObservation.Record(
                            id: Guid.NewGuid(),
                            plantProfileId: profile.Id,
                            tenantId: order.TenantId,
                            farmId: farmId,
                            surveyOrderId: order.Id,
                            farmBaseMapVersionId: draft.Id,
                            observedLocation: targetLocation,
                            observedDistanceM: 0.0,
                            candidateId: c.CandidateId,
                            observedBy: actorId,
                            observedAt: now);
                        dbContext.PlantProfileObservations.Add(observation);

                        candidateToProfileMap[c.CandidateId] = profile.Id;

                        plantMappings.Add(new PublishedPlantMappingV3(
                            PlantId: existingPlant.Id,
                            SourceCandidateId: c.CandidateId,
                            Position: new GeoJsonPointV3("Point", [targetLocation.X, targetLocation.Y]),
                            RowIndex: existingPlant.RowIndex ?? row,
                            ColumnIndex: existingPlant.ColumnIndex ?? col,
                            LifecycleStatus: PublishedPlantLifecycleStatusesV3.Active,
                            AppliedPlantChangeReportId: null));
                    }
                    else if (c.ReviewDecision == MappingCandidateReviewDecision.Relocate)
                    {
                        var targetPlantId = c.CorrectedMatchedPlantId ?? c.ProposedMatchedPlantId;
                        if (!targetPlantId.HasValue)
                        {
                            return Result.Failure<FarmBaseMapPublishedResult>(
                                AppError.Validation(
                                    "MappingCandidate.MissingRelocatePlantId",
                                    $"Candidate {c.CandidateId} has Relocate decision but lacks target plant ID."));
                        }

                        var existingPlant = await dbContext.Plants
                            .SingleOrDefaultAsync(p => p.Id == targetPlantId.Value, ct);

                        if (existingPlant is null)
                        {
                            return Result.Failure<FarmBaseMapPublishedResult>(
                                AppError.NotFound(
                                    "Plant.NotFound",
                                    $"Target plant {targetPlantId.Value} for candidate {c.CandidateId} was not found."));
                        }

                        var profile = await dbContext.PlantProfiles
                            .FirstOrDefaultAsync(p => p.FarmId == farmId &&
                                (p.Id == existingPlant.PlantProfileId || p.PhysicalPoleIdentifier == existingPlant.PlantCode) &&
                                p.Status == PlantProfileStatus.Active, ct);

                        if (profile is null)
                        {
                            return Result.Failure<FarmBaseMapPublishedResult>(
                                AppError.NotFound(
                                    "PlantProfile.NotFound",
                                    $"Active plant profile for plant {targetPlantId.Value} was not found."));
                        }

                        var oldLocation = existingPlant.Location;
                        var oldRow = existingPlant.RowIndex;
                        var oldCol = existingPlant.ColumnIndex;

                        var distanceM = oldLocation != null
                            ? GeodesicSpatialCalculator.CalculateDistanceMeters(
                                oldLocation.Y, oldLocation.X,
                                pos.Y, pos.X)
                            : 0.0;

                        if (distanceM > matchingThresholdM)
                        {
                            return Result.Failure<FarmBaseMapPublishedResult>(
                                AppError.Conflict(
                                    "MappingCandidate.RelocationExceedsTolerance",
                                    $"Candidate {c.CandidateId} relocation distance ({distanceM:F2}m) exceeds spatial policy threshold ({matchingThresholdM:F2}m)."));
                        }

                        profile.Relocate(pos, draft.Id, matchingThresholdM, now);

                        existingPlant.ApplyPublishedFarmMapPosition(
                            farmBaseMapVersionId: draft.Id,
                            location: pos,
                            rowIndex: row,
                            columnIndex: col,
                            locationAccuracyM: 0.1m,
                            positionConfidence: c.PositionConfidence,
                            mappedAt: now);
                        existingPlant.LinkPlantProfile(profile.Id);

                        var changeEvt = PlantChangeEvent.Relocated(
                            farmId: farmId,
                            plantProfileId: profile.Id,
                            sourceSurveyOrderId: order.Id,
                            missionId: batch.MissionId,
                            plantId: existingPlant.Id,
                            farmBaseMapVersionId: draft.Id,
                            spatialPolicyVersionId: effectivePolicy.Id,
                            managerDecisionId: c.Id,
                            oldLocation: oldLocation,
                            newLocation: pos,
                            oldRowIndex: oldRow,
                            newRowIndex: row,
                            oldColumnIndex: oldCol,
                            newColumnIndex: col,
                            lifecycleStatus: existingPlant.LifecycleStatus,
                            actorId: actorId,
                            createdAt: now);
                        dbContext.PlantChangeEvents.Add(changeEvt);

                        candidateToProfileMap[c.CandidateId] = profile.Id;

                        plantMappings.Add(new PublishedPlantMappingV3(
                            PlantId: existingPlant.Id,
                            SourceCandidateId: c.CandidateId,
                            Position: new GeoJsonPointV3("Point", [pos.X, pos.Y]),
                            RowIndex: row,
                            ColumnIndex: col,
                            LifecycleStatus: PublishedPlantLifecycleStatusesV3.Active,
                            AppliedPlantChangeReportId: null));
                    }
                    else if (c.ReviewDecision == MappingCandidateReviewDecision.Replace)
                    {
                        var targetPlantId = c.CorrectedMatchedPlantId ?? c.ProposedMatchedPlantId;
                        if (!targetPlantId.HasValue)
                        {
                            return Result.Failure<FarmBaseMapPublishedResult>(
                                AppError.Validation(
                                    "MappingCandidate.MissingReplacePlantId",
                                    $"Candidate {c.CandidateId} has Replace decision but lacks target plant ID."));
                        }

                        var oldPlant = await dbContext.Plants
                            .SingleOrDefaultAsync(p => p.Id == targetPlantId.Value, ct);

                        if (oldPlant is null)
                        {
                            return Result.Failure<FarmBaseMapPublishedResult>(
                                AppError.NotFound(
                                    "Plant.NotFound",
                                    $"Target plant {targetPlantId.Value} for replacement was not found."));
                        }

                        var oldProfile = await dbContext.PlantProfiles
                            .FirstOrDefaultAsync(p => p.FarmId == farmId &&
                                (p.Id == oldPlant.PlantProfileId || p.PhysicalPoleIdentifier == oldPlant.PlantCode) &&
                                p.Status == PlantProfileStatus.Active, ct);

                        if (oldProfile is null)
                        {
                            return Result.Failure<FarmBaseMapPublishedResult>(
                                AppError.NotFound(
                                    "PlantProfile.NotFound",
                                    $"Active plant profile for target plant {targetPlantId.Value} was not found."));
                        }

                        oldPlant.Retire(PlantLifecycleStatus.Removed, now);

                        var newPlantId = Guid.NewGuid();
                        var newProfileId = Guid.NewGuid();
                        var poleCode = $"P-{farmId.ToString()[..4]}-{row:D3}-{col:D3}-{newProfileId.ToString()[..4]}";

                        var newProfile = oldProfile.ReplaceWithNewProfile(
                            newProfileId: newProfileId,
                            newPoleIdentifier: poleCode,
                            newLocation: pos,
                            mapVersionId: draft.Id,
                            managerDecisionId: c.Id,
                            reason: "Replaced during base map internal review",
                            at: now);
                        dbContext.PlantProfiles.Add(newProfile);

                        var newPlant = Plant.CreateFromFarmMapping(
                            id: newPlantId,
                            farmId: farmId,
                            plantCode: poleCode,
                            location: pos,
                            farmBaseMapVersionId: draft.Id,
                            rowIndex: row,
                            columnIndex: col,
                            locationAccuracyM: 0.1m,
                            positionConfidence: c.PositionConfidence,
                            healthLevelId: healthId,
                            sourceMissionId: batch.MissionId,
                            mappedAt: now);
                        newPlant.LinkPlantProfile(newProfile.Id);
                        newPlant.LinkReplacedPlant(oldPlant.Id);
                        dbContext.Plants.Add(newPlant);

                        var changeEvt = PlantChangeEvent.Replaced(
                            farmId: farmId,
                            plantProfileId: newProfile.Id,
                            predecessorPlantProfileId: oldProfile.Id,
                            sourceSurveyOrderId: order.Id,
                            missionId: batch.MissionId,
                            plantId: newPlant.Id,
                            farmBaseMapVersionId: draft.Id,
                            spatialPolicyVersionId: effectivePolicy.Id,
                            managerDecisionId: c.Id,
                            oldLocation: oldPlant.Location,
                            newLocation: pos,
                            oldRowIndex: oldPlant.RowIndex,
                            newRowIndex: row,
                            oldColumnIndex: oldPlant.ColumnIndex,
                            newColumnIndex: col,
                            actorId: actorId,
                            createdAt: now);
                        dbContext.PlantChangeEvents.Add(changeEvt);

                        candidateToProfileMap[c.CandidateId] = newProfile.Id;

                        plantMappings.Add(new PublishedPlantMappingV3(
                            PlantId: newPlant.Id,
                            SourceCandidateId: c.CandidateId,
                            Position: new GeoJsonPointV3("Point", [pos.X, pos.Y]),
                            RowIndex: row,
                            ColumnIndex: col,
                            LifecycleStatus: PublishedPlantLifecycleStatusesV3.Active,
                            AppliedPlantChangeReportId: null));
                    }
                    else // CreateNew
                    {
                        var plantId = Guid.NewGuid();
                        var profileId = Guid.NewGuid();
                        var plantCode = $"P-{farmId.ToString()[..4]}-{row:D3}-{col:D3}-{plantId.ToString()[..4]}";

                        var plant = Plant.CreateFromFarmMapping(
                            id: plantId,
                            farmId: farmId,
                            plantCode: plantCode,
                            location: pos,
                            farmBaseMapVersionId: draft.Id,
                            rowIndex: row,
                            columnIndex: col,
                            locationAccuracyM: 0.1m,
                            positionConfidence: c.PositionConfidence,
                            healthLevelId: healthId,
                            sourceMissionId: batch.MissionId,
                            mappedAt: now);

                        var newProfile = PlantProfile.Create(
                            id: profileId,
                            tenantId: order.TenantId,
                            farmId: farmId,
                            physicalPoleIdentifier: plantCode,
                            baselineLocation: pos,
                            baselineMapVersionId: draft.Id,
                            createdAt: now);

                        plant.LinkPlantProfile(newProfile.Id);
                        dbContext.Plants.Add(plant);
                        dbContext.PlantProfiles.Add(newProfile);

                        var changeEvt = PlantChangeEvent.CreatedWithLineage(
                            farmId: farmId,
                            plantProfileId: newProfile.Id,
                            sourceSurveyOrderId: order.Id,
                            missionId: batch.MissionId,
                            plantId: plant.Id,
                            farmBaseMapVersionId: draft.Id,
                            spatialPolicyVersionId: effectivePolicy.Id,
                            location: pos,
                            rowIndex: row,
                            columnIndex: col,
                            actorId: actorId,
                            createdAt: now);
                        dbContext.PlantChangeEvents.Add(changeEvt);

                        candidateToProfileMap[c.CandidateId] = newProfile.Id;

                        plantMappings.Add(new PublishedPlantMappingV3(
                            PlantId: plantId,
                            SourceCandidateId: c.CandidateId,
                            Position: new GeoJsonPointV3("Point", [pos.X, pos.Y]),
                            RowIndex: row,
                            ColumnIndex: col,
                            LifecycleStatus: PublishedPlantLifecycleStatusesV3.Active,
                            AppliedPlantChangeReportId: null));
                    }
                }

                // Internal Approval transition (Status becomes InternalApproved, NOT Published)
                draft.ApproveInternal(actorId, now);

                batch.Approve(actorId, "Farm base map approved internally with reviewed plant identities", now);

                // Authoritative Billable Poles Count
                var billableProfiles = new HashSet<Guid>();
                foreach (var c in publishableCandidates)
                {
                    var pos = c.CorrectedPosition ?? c.OriginalPosition;
                    if (boundary != null && boundary.Geometry.Contains(pos))
                    {
                        if (candidateToProfileMap.TryGetValue(c.CandidateId, out var profileId))
                        {
                            billableProfiles.Add(profileId);
                        }
                    }
                }
                var billableCount = billableProfiles.Count;

                var latestSnapshot = await dbContext.SurveyOrderPoleCountSnapshots
                    .Where(s => s.SurveyOrderId == order.Id)
                    .OrderByDescending(s => s.RevisionNumber)
                    .FirstOrDefaultAsync(ct);

                var nextRevision = (latestSnapshot?.RevisionNumber ?? 0) + 1;

                var snapshot = SurveyOrderPoleCountSnapshot.Create(
                    id: Guid.NewGuid(),
                    surveyOrderId: order.Id,
                    tenantId: order.TenantId,
                    farmId: order.FarmId,
                    missionId: batch.MissionId,
                    captureAttemptId: null,
                    farmBaseMapVersionId: draft.Id,
                    farmBoundaryVersionId: batch.FarmBoundaryVersionId,
                    mappingCandidateBatchId: batch.Id,
                    spatialPolicyVersionId: effectivePolicy.Id,
                    verifiedBillablePoleCount: billableCount,
                    verifiedBy: actorId,
                    verifiedAt: now,
                    revisionNumber: nextRevision,
                    predecessorSnapshotId: latestSnapshot?.Id ?? order.VerifiedPoleCountSnapshotId);

                dbContext.SurveyOrderPoleCountSnapshots.Add(snapshot);

                var confirmedCount = ConfirmedSurveyPoleCount.Create(validation.ConfirmedPublishablePoleCount);
                order.RecordBaselinePublication(
                    farmBaseMapVersionId: draft.Id,
                    confirmedPoleCount: confirmedCount,
                    confirmedBy: actorId,
                    confirmedAt: now,
                    farmBoundaryVersionId: batch.FarmBoundaryVersionId);

                order.SetBaselineMapVersion(baselineMapVersionId, order.Version, now);
                order.RecordVerifiedPoleCount(snapshot, order.Version);

                var publishedEvent = new FarmBaseMapPublishedV3(
                    CausationId: Guid.NewGuid(),
                    PublicationId: Guid.NewGuid(),
                    SurveyOrderId: order.Id,
                    SourceMissionId: batch.MissionId,
                    FarmId: farmId,
                    FarmBoundaryVersionId: batch.FarmBoundaryVersionId,
                    FarmBaseMapVersionId: draft.Id,
                    VersionNumber: draft.VersionNumber,
                    ConfirmedSurveyPoleCount: validation.ConfirmedPublishablePoleCount,
                    ConfirmedBy: actorId,
                    PublishedAt: now,
                    PlantMappings: plantMappings,
                    AppliedPlantChangeReportIds: []);

                var envelope = IntegrationEventEnvelopeFactory.Create(
                    descriptor: IntegrationEventDescriptors.FarmBaseMapPublishedV3,
                    messageId: Guid.NewGuid(),
                    correlationId: Guid.NewGuid(),
                    tenantId: order.TenantId,
                    actorId: actorId,
                    occurredAt: now,
                    payload: publishedEvent,
                    causationId: publishedEvent.CausationId);

                var outboxMessage = outboxMessageFactory.Create(
                    envelope,
                    envelope.EventType,
                    partitionKey: farmId.ToString());

                dbContext.OutboxMessages.Add(outboxMessage);

                using var auditDoc = JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    draft.Id,
                    draft.VersionNumber,
                    validation.ConfirmedPublishablePoleCount,
                    VerifiedBillablePoleCount = billableCount,
                    SnapshotId = snapshot.Id,
                    ApprovedAt = now
                }));

                auditWriter.AddUserAction(
                    sink: dbContext,
                    tenantId: order.TenantId,
                    farmId: farmId,
                    actorId: actorId,
                    correlationId: envelope.CorrelationId,
                    entityType: "FarmBaseMapVersion",
                    entityId: draft.Id,
                    action: "INTERNAL_APPROVE",
                    oldData: null,
                    newData: auditDoc,
                    createdAt: now);

                await dbContext.SaveChangesAsync(ct);

                return Result.Success(new FarmBaseMapPublishedResult(
                    FarmBaseMapVersionId: draft.Id,
                    VersionNumber: draft.VersionNumber,
                    ConfirmedSurveyPoleCount: validation.ConfirmedPublishablePoleCount,
                    PublishedAt: now));
            }, cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Database update error while approving farm base map for farm {FarmId}, draft {DraftId}: {Message} -> {InnerMessage}",
                farmId, draftId, ex.Message, ex.InnerException?.Message);
            return Result.Failure<FarmBaseMapPublishedResult>(
                AppError.Conflict(
                    "FarmBaseMapPublication.DatabaseError",
                    $"Database constraint failure: {ex.InnerException?.Message ?? ex.Message}"));
        }
        catch (SurveyDomainException ex)
        {
            logger.LogWarning(ex, "Domain rule violation approving farm base map for farm {FarmId}, draft {DraftId}: {Code} - {Message}",
                farmId, draftId, ex.Code, ex.Message);
            return Result.Failure<FarmBaseMapPublishedResult>(
                AppError.Conflict(ex.Code, ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error approving farm base map for farm {FarmId}, draft {DraftId}", farmId, draftId);
            return Result.Failure<FarmBaseMapPublishedResult>(
                AppError.Failure(
                    "FarmBaseMapPublication.Failed",
                    $"Approval failed: {ex.Message}"));
        }
    }
}
