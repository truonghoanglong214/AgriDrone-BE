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
using NetTopologySuite.Geometries;

namespace AgriDrone.Database.Mapping;

internal sealed class FarmBaseMapPublicationOrchestrator(
    MappingPublicationDbContext dbContext,
    IBaselineMappingBatchValidator validator,
    OutboxMessageFactory outboxMessageFactory,
    IAuditWriter auditWriter,
    TimeProvider timeProvider)
    : IFarmBaseMapPublicationOrchestrator
{
    public async Task<Result<FarmBaseMapPublishedResult>> PublishAsync(
        Guid farmId,
        Guid draftId,
        Guid actorId,
        CancellationToken cancellationToken)
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
                .Include(b => b.Zones)
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

            // Supersede existing published farm base map versions to respect partial unique index
            var existingPublishedMaps = await dbContext.FarmBaseMapVersions
                .Where(v => v.FarmId == farmId && v.Status == FarmBaseMapStatus.Published)
                .ToListAsync(ct);

            foreach (var prevMap in existingPublishedMaps)
            {
                prevMap.Supersede(now);
            }

            // Supersede existing confirmed zone map versions for zones in this batch
            var batchZoneIds = batch.Zones.Select(z => z.ZoneId).ToList();
            var existingZoneMaps = await dbContext.ZoneMapVersions
                .Where(zm => batchZoneIds.Contains(zm.ZoneId) && zm.Status == MapVersionStatus.Confirmed)
                .ToListAsync(ct);

            foreach (var prevZoneMap in existingZoneMaps)
            {
                prevZoneMap.Supersede();
            }

            var defaultHealth = await dbContext.HealthLevels
                .Where(h => h.IsActive)
                .OrderBy(h => h.Rank)
                .FirstOrDefaultAsync(ct);
            var healthId = defaultHealth?.Id ?? Guid.Parse("10000000-0000-0000-0000-000000000001");

            var publishedZones = new List<PublishedZoneMapV3>();

            foreach (var z in batch.Zones)
            {
                var zoneMapId = Guid.NewGuid();
                var zoneMap = ZoneMapVersion.CreateDraftForFarmMap(
                    id: zoneMapId,
                    farmId: farmId,
                    zoneId: z.ZoneId,
                    farmBaseMapVersionId: draft.Id,
                    sourceMissionId: batch.MissionId,
                    sourceApprovalId: Guid.NewGuid(),
                    versionNumber: draft.VersionNumber,
                    gridBearingDeg: (decimal)z.GridBearingDeg,
                    rowSpacingM: (decimal)z.RowSpacingM,
                    plantSpacingM: (decimal)z.PlantSpacingM,
                    algorithmVersion: batch.AlgorithmVersion,
                    parameters: JsonDocument.Parse("{}"),
                    createdAt: now);

                zoneMap.Confirm(actorId, now);
                dbContext.ZoneMapVersions.Add(zoneMap);

                var zoneCandidates = batch.Candidates
                    .Where(c => c.ZoneId == z.ZoneId &&
                                c.ReviewDecision is MappingCandidateReviewDecision.CreateNew
                                                 or MappingCandidateReviewDecision.MatchExisting
                                                 or MappingCandidateReviewDecision.Relocate
                                                 or MappingCandidateReviewDecision.Replace)
                    .ToList();

                var plantMappings = new List<PublishedPlantMappingV3>();

                foreach (var c in zoneCandidates)
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

                        existingPlant.ApplyPublishedMapPosition(
                            mapVersionId: zoneMapId,
                            location: existingPlant.Location ?? pos,
                            rowIndex: existingPlant.RowIndex ?? row,
                            columnIndex: existingPlant.ColumnIndex ?? col,
                            locationAccuracyM: existingPlant.LocationAccuracyM,
                            positionConfidence: c.PositionConfidence,
                            mappedAt: now);

                        var targetLocation = existingPlant.Location ?? pos;
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

                        existingPlant.ApplyPublishedMapPosition(
                            mapVersionId: zoneMapId,
                            location: pos,
                            rowIndex: row,
                            columnIndex: col,
                            locationAccuracyM: 0.1m,
                            positionConfidence: c.PositionConfidence,
                            mappedAt: now);

                        dbContext.PlantChangeEvents.Add(
                            PlantChangeEvent.MappingPositionChanged(
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
                                createdAt: now));

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
                        if (targetPlantId.HasValue)
                        {
                            var oldPlant = await dbContext.Plants
                                .SingleOrDefaultAsync(p => p.Id == targetPlantId.Value, ct);
                            if (oldPlant is not null)
                            {
                                oldPlant.Retire(PlantLifecycleStatus.Removed, now);
                            }
                        }

                        var plantId = Guid.NewGuid();
                        var plantCode = $"P-{z.ZoneId.ToString()[..4]}-{row:D3}-{col:D3}-{plantId.ToString()[..4]}";

                        var plant = Plant.CreateFromMapping(
                            id: plantId,
                            farmId: farmId,
                            zoneId: z.ZoneId,
                            plantCode: plantCode,
                            location: pos,
                            mapVersionId: zoneMapId,
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

                        dbContext.PlantChangeEvents.Add(
                            PlantChangeEvent.MappingCreated(
                                farmId,
                                batch.MissionId,
                                plantId,
                                pos,
                                row,
                                col,
                                actorId,
                                now));

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
                        var plantCode = $"P-{z.ZoneId.ToString()[..4]}-{row:D3}-{col:D3}-{plantId.ToString()[..4]}";

                        var plant = Plant.CreateFromMapping(
                            id: plantId,
                            farmId: farmId,
                            zoneId: z.ZoneId,
                            plantCode: plantCode,
                            location: pos,
                            mapVersionId: zoneMapId,
                            rowIndex: row,
                            columnIndex: col,
                            locationAccuracyM: 0.1m,
                            positionConfidence: c.PositionConfidence,
                            healthLevelId: healthId,
                            sourceMissionId: batch.MissionId,
                            mappedAt: now);

                        dbContext.Plants.Add(plant);

                        dbContext.PlantChangeEvents.Add(
                            PlantChangeEvent.MappingCreated(
                                farmId,
                                batch.MissionId,
                                plantId,
                                pos,
                                row,
                                col,
                                actorId,
                                now));

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

                publishedZones.Add(new PublishedZoneMapV3(
                    ZoneId: z.ZoneId,
                    ZoneMapVersionId: zoneMapId,
                    VersionNumber: draft.VersionNumber,
                    PlantMappings: plantMappings));
            }

            draft.Publish(actorId, now);

            batch.Approve(actorId, "Farm base map published with reviewed plant identities", now);

            var confirmedCount = ConfirmedSurveyPoleCount.Create(validation.ConfirmedPublishablePoleCount);
            order.RecordBaselinePublication(
                farmBaseMapVersionId: draft.Id,
                confirmedPoleCount: confirmedCount,
                confirmedBy: actorId,
                confirmedAt: now);

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
                Zones: publishedZones,
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
}
