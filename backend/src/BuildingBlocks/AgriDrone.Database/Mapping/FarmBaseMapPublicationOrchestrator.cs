using System.Text.Json;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.Modules.Farms.Application.Abstractions.Maps;
using AgriDrone.Modules.Farms.Application.Services;
using AgriDrone.Modules.Farms.Domain.Maps;
using AgriDrone.Modules.Farms.Domain.Maps.Staging;
using AgriDrone.Modules.Plants.Domain.Mapping;
using AgriDrone.Modules.Plants.Domain.Plants;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Messaging.Outbox;
using AgriDrone.SharedKernel.Application;
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
                        AppError.Conflict("FarmBaseMapDraft.InvalidStatus", $"Only draft base maps can be published; current status is '{draft.Status}'."));
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
                        AppError.Validation("MappingCandidateBatch.ValidationFailed", $"Cannot publish base map due to validation errors: {errorMessages}"));
                }

                if (validation.ConfirmedPublishablePoleCount <= 0)
                {
                    return Result.Failure<FarmBaseMapPublishedResult>(
                        AppError.Validation(
                            "FarmBaseMapDraft.NoPublishablePoles",
                            "At least one candidate must have an accepted review decision (CreateNew, MatchExisting, Relocate, Replace) to publish."));
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
                        AppError.Conflict("SurveyOrder.InvalidStatus", $"Survey order {order.Id} is in status '{order.Status}' and cannot record baseline publication."));
                }

                if (batch.Candidates.Any(c => c.ReviewDecision == MappingCandidateReviewDecision.Pending))
                {
                    return Result.Failure<FarmBaseMapPublishedResult>(
                        AppError.Conflict(
                            "MappingCandidateBatch.PendingReviewRemaining",
                            "Cannot publish farm base map while candidates remain in Pending review decision."));
                }

                var now = timeProvider.GetUtcNow();
                if (order.UpdatedAt != default && now < order.UpdatedAt)
                {
                    now = order.UpdatedAt.AddMilliseconds(1);
                }

                // Supersede existing published farm base map versions to respect partial unique index
                var existingPublishedMaps = await dbContext.FarmBaseMapVersions
                    .Where(v => v.FarmId == farmId && v.Status == FarmBaseMapStatus.Published)
                    .ToListAsync(ct);

                foreach (var prevMap in existingPublishedMaps)
                {
                    prevMap.Supersede(now);
                }

                // Determine baseline lineage
                var existingBaselineMap = await dbContext.FarmBaseMapVersions
                    .Where(v => v.FarmId == farmId && v.IsBaseline)
                    .FirstOrDefaultAsync(ct);

                var latestPublishedMap = existingPublishedMaps
                    .OrderByDescending(v => v.VersionNumber)
                    .FirstOrDefault();

                Guid baselineMapVersionId;
                if (existingBaselineMap is null)
                {
                    // First order creates baseline
                    draft.ApproveInternal(actorId, now);
                    draft.SetLineage(isBaseline: true, predecessorVersionId: null, baselineMapVersionId: draft.Id);
                    baselineMapVersionId = draft.Id;
                }
                else
                {
                    // Follow-up order references existing baseline
                    draft.ApproveInternal(actorId, now);
                    draft.SetLineage(
                        isBaseline: false,
                        predecessorVersionId: latestPublishedMap?.Id ?? existingBaselineMap.Id,
                        baselineMapVersionId: existingBaselineMap.Id);
                    baselineMapVersionId = existingBaselineMap.Id;
                }

                var effectivePolicy = await dbContext.SpatialPolicies
                    .Where(p => p.StreamKey == "DEFAULT" &&
                                p.EffectiveFrom <= now &&
                                (p.EffectiveTo == null || p.EffectiveTo > now))
                    .OrderByDescending(p => p.VersionNumber)
                    .FirstOrDefaultAsync(ct);

                var matchingThresholdM = effectivePolicy?.MatchingDistanceThresholdM ?? 1.5;

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

                        existingPlant.ApplyPublishedFarmMapPosition(
                            farmBaseMapVersionId: draft.Id,
                            location: existingPlant.Location ?? pos,
                            rowIndex: existingPlant.RowIndex ?? row,
                            columnIndex: existingPlant.ColumnIndex ?? col,
                            locationAccuracyM: existingPlant.LocationAccuracyM,
                            positionConfidence: c.PositionConfidence,
                            mappedAt: now);

                        var targetLocation = existingPlant.Location ?? pos;
                        var profile = await dbContext.PlantProfiles
                            .FirstOrDefaultAsync(p => p.FarmId == farmId && p.PhysicalPoleIdentifier == existingPlant.PlantCode && p.Status == PlantProfileStatus.Active, ct);
                        if (profile is not null)
                        {
                            profile.Relocate(targetLocation, draft.Id, 0, matchingThresholdM, now);
                        }

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

                        var oldLocation = existingPlant.Location;
                        var oldRow = existingPlant.RowIndex;
                        var oldCol = existingPlant.ColumnIndex;

                        existingPlant.ApplyPublishedFarmMapPosition(
                            farmBaseMapVersionId: draft.Id,
                            location: pos,
                            rowIndex: row,
                            columnIndex: col,
                            locationAccuracyM: 0.1m,
                            positionConfidence: c.PositionConfidence,
                            mappedAt: now);

                        var distanceM = oldLocation != null ? (double)pos.Distance(oldLocation) * 111_000 : 0.0;
                        var profile = await dbContext.PlantProfiles
                            .FirstOrDefaultAsync(p => p.FarmId == farmId && p.PhysicalPoleIdentifier == existingPlant.PlantCode && p.Status == PlantProfileStatus.Active, ct);
                        if (profile is not null)
                        {
                            if (distanceM <= matchingThresholdM)
                            {
                                profile.Relocate(pos, draft.Id, distanceM, matchingThresholdM, now);
                            }
                            else
                            {
                                profile.MarkNeedsReview($"Relocation distance {distanceM:F2}m exceeds threshold {matchingThresholdM:F2}m", now);
                            }
                        }

                        var changeEvt = PlantChangeEvent.MappingPositionChanged(
                            farmId: farmId,
                            missionId: batch.MissionId,
                            plantId: existingPlant.Id,
                            oldLocation: oldLocation,
                            newLocation: pos,
                            oldRowIndex: oldRow,
                            newRowIndex: row,
                            oldColumnIndex: oldCol,
                            newColumnIndex: col,
                            lifecycleStatus: existingPlant.LifecycleStatus,
                            actorId: actorId,
                            createdAt: now);
                        changeEvt.SetLineageMetadata(profile?.Id, order.Id, effectivePolicy?.Id, null);
                        dbContext.PlantChangeEvents.Add(changeEvt);

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
                        PlantProfile? newProfile = null;
                        if (targetPlantId.HasValue)
                        {
                            var oldPlant = await dbContext.Plants
                                .SingleOrDefaultAsync(p => p.Id == targetPlantId.Value, ct);
                            if (oldPlant is not null)
                            {
                                oldPlant.Retire(PlantLifecycleStatus.Removed, now);
                                var oldProfile = await dbContext.PlantProfiles
                                    .FirstOrDefaultAsync(p => p.FarmId == farmId && p.PhysicalPoleIdentifier == oldPlant.PlantCode && p.Status == PlantProfileStatus.Active, ct);
                                if (oldProfile is not null)
                                {
                                    newProfile = oldProfile.ReplaceWithNewProfile(
                                        Guid.NewGuid(),
                                        $"P-{farmId.ToString()[..4]}-{row:D3}-{col:D3}-{Guid.NewGuid().ToString()[..4]}",
                                        pos,
                                        draft.Id,
                                        "Replaced during base map publication",
                                        now);
                                    dbContext.PlantProfiles.Add(newProfile);
                                }
                            }
                        }

                        var plantId = Guid.NewGuid();
                        var plantCode = newProfile?.PhysicalPoleIdentifier ?? $"P-{farmId.ToString()[..4]}-{row:D3}-{col:D3}-{plantId.ToString()[..4]}";

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

                        if (targetPlantId.HasValue)
                        {
                            plant.LinkReplacedPlant(targetPlantId.Value);
                        }

                        dbContext.Plants.Add(plant);

                        if (newProfile is null)
                        {
                            newProfile = PlantProfile.Create(
                                Guid.NewGuid(),
                                order.TenantId,
                                farmId,
                                plantCode,
                                pos,
                                baselineMapVersionId,
                                now);
                            dbContext.PlantProfiles.Add(newProfile);
                        }

                        var changeEvt = PlantChangeEvent.MappingCreated(
                            farmId,
                            batch.MissionId,
                            plantId,
                            pos,
                            row,
                            col,
                            actorId,
                            now);
                        changeEvt.SetLineageMetadata(newProfile.Id, order.Id, effectivePolicy?.Id, null);
                        dbContext.PlantChangeEvents.Add(changeEvt);

                        plantMappings.Add(new PublishedPlantMappingV3(
                            PlantId: plantId,
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

                        dbContext.Plants.Add(plant);

                        var newProfile = PlantProfile.Create(
                            Guid.NewGuid(),
                            order.TenantId,
                            farmId,
                            plantCode,
                            pos,
                            baselineMapVersionId,
                            now);
                        dbContext.PlantProfiles.Add(newProfile);

                        var changeEvt = PlantChangeEvent.MappingCreated(
                            farmId,
                            batch.MissionId,
                            plantId,
                            pos,
                            row,
                            col,
                            actorId,
                            now);
                        changeEvt.SetLineageMetadata(newProfile.Id, order.Id, effectivePolicy?.Id, null);
                        dbContext.PlantChangeEvents.Add(changeEvt);

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

                draft.Publish(actorId, now);

                batch.Approve(actorId, "Farm base map published with reviewed plant identities", now);

                var confirmedCount = ConfirmedSurveyPoleCount.Create(validation.ConfirmedPublishablePoleCount);
                order.RecordBaselinePublication(
                    farmBaseMapVersionId: draft.Id,
                    confirmedPoleCount: confirmedCount,
                    confirmedBy: actorId,
                    confirmedAt: now,
                    farmBoundaryVersionId: batch.FarmBoundaryVersionId);

                order.SetBaselineMapVersion(baselineMapVersionId);

                // Calculate billable poles within boundary
                var billableCount = 0;
                foreach (var c in publishableCandidates)
                {
                    var pos = c.CorrectedPosition ?? c.OriginalPosition;
                    if (boundary != null && boundary.Geometry.Contains(pos))
                    {
                        billableCount++;
                    }
                }
                order.RecordVerifiedBillablePoleCount(billableCount, actorId, now);

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
                    PublishedAt = now
                }));

                auditWriter.AddUserAction(
                    sink: dbContext,
                    tenantId: order.TenantId,
                    farmId: farmId,
                    actorId: actorId,
                    correlationId: envelope.CorrelationId,
                    entityType: "FarmBaseMapVersion",
                    entityId: draft.Id,
                    action: "PUBLISH",
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
            logger.LogError(ex, "Database update error while publishing farm base map for farm {FarmId}, draft {DraftId}: {Message} -> {InnerMessage}",
                farmId, draftId, ex.Message, ex.InnerException?.Message);
            return Result.Failure<FarmBaseMapPublishedResult>(
                AppError.Conflict(
                    "FarmBaseMapPublication.DatabaseError",
                    $"Database constraint failure: {ex.InnerException?.Message ?? ex.Message}"));
        }
        catch (SurveyDomainException ex)
        {
            logger.LogWarning(ex, "Domain rule violation publishing farm base map for farm {FarmId}, draft {DraftId}: {Code} - {Message}",
                farmId, draftId, ex.Code, ex.Message);
            return Result.Failure<FarmBaseMapPublishedResult>(
                AppError.Conflict(ex.Code, ex.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error publishing farm base map for farm {FarmId}, draft {DraftId}", farmId, draftId);
            return Result.Failure<FarmBaseMapPublishedResult>(
                AppError.Failure(
                    "FarmBaseMapPublication.Failed",
                    $"Publication failed: {ex.Message}"));
        }
    }
}
