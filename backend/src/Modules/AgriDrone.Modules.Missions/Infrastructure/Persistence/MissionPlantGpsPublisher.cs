using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Messaging.Validation;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.SharedInfrastructure.Messaging.Outbox;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Infrastructure.Persistence;

internal sealed class MissionPlantGpsPublisher(
    MissionsDbContext db,
    OutboxMessageFactory outboxMessageFactory,
    TimeProvider timeProvider) : IMissionPlantGpsPublisher
{
    public async Task<MissionPlantGpsPublishResult> PublishAsync(
        Guid tenantId,
        Guid farmId,
        Guid missionId,
        Guid actorId,
        Guid correlationId,
        Guid idempotencyKey,
        BaselineMappingCandidatesReadyV3 payload,
        CancellationToken cancellationToken)
    {
        var existingMessage = await db.OutboxMessages.AsNoTracking()
            .SingleOrDefaultAsync(message => message.MessageId == idempotencyKey,
                cancellationToken);
        if (existingMessage is not null)
        {
            return existingMessage.EventType ==
                   IntegrationEventTypes.BaselineMappingCandidatesReadyV3
                ? new MissionPlantGpsPublishResult(
                    MissionPlantGpsPublishStatus.Queued, idempotencyKey)
                : new MissionPlantGpsPublishResult(
                    MissionPlantGpsPublishStatus.Conflict,
                    Errors: ["Idempotency-Key has already been used for another event."]);
        }

        var mission = await db.DroneMissions.AsNoTracking()
            .Where(item => item.Id == missionId && item.FarmId == farmId &&
                           item.TenantId == tenantId)
            .Select(item => new
            {
                item.SurveyOrderId,
                item.ZoneId,
                item.ScopeZoneIds
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (mission is null)
        {
            return new MissionPlantGpsPublishResult(
                MissionPlantGpsPublishStatus.NotFound);
        }

        if (mission.SurveyOrderId is null ||
            mission.SurveyOrderId != payload.SurveyOrderId ||
            payload.MissionId != missionId || payload.FarmId != farmId)
        {
            return new MissionPlantGpsPublishResult(
                MissionPlantGpsPublishStatus.Conflict,
                Errors: ["Payload mission, farm, or survey order does not match the stored mission."]);
        }

        if (payload.Zones is null || payload.Zones.Count == 0 ||
            payload.Zones.Any(zone => zone is null || zone.Candidates is null))
        {
            return new MissionPlantGpsPublishResult(
                MissionPlantGpsPublishStatus.Invalid,
                Errors: ["Payload must contain zones and candidate lists."]);
        }

        var missionZoneIds = (mission.ScopeZoneIds ?? [])
            .Where(zoneId => zoneId != Guid.Empty)
            .ToHashSet();
        if (mission.ZoneId is Guid primaryZoneId && primaryZoneId != Guid.Empty)
            missionZoneIds.Add(primaryZoneId);
        if (payload.Zones.Any(zone => !missionZoneIds.Contains(zone.ZoneId)))
        {
            return new MissionPlantGpsPublishResult(
                MissionPlantGpsPublishStatus.Conflict,
                Errors: ["Payload zones must belong to this mission."]);
        }

        var storedRows = await db.Database.SqlQueryRaw<MissionPlantGpsRow>(
                "SELECT tree_id AS \"TreeId\", row_number AS \"Row\", " +
                "col_number AS \"Col\", latitude AS \"Latitude\", " +
                "longitude AS \"Longitude\", confidence AS \"Confidence\", " +
                "status AS \"Status\", source AS \"Source\" " +
                "FROM mission.mission_plant_gps WHERE mission_id = {0}",
                missionId)
            .ToListAsync(cancellationToken);
        if (storedRows.Count == 0)
        {
            return new MissionPlantGpsPublishResult(
                MissionPlantGpsPublishStatus.Conflict,
                Errors: ["No saved plant GPS rows were found for this mission."]);
        }

        var storedByTreeId = storedRows.ToDictionary(
            row => row.TreeId, StringComparer.Ordinal);
        var requestedCandidates = payload.Zones
            .SelectMany(zone => zone.Candidates ?? Array.Empty<MappingCandidateV3>())
            .ToArray();
        var treeIds = requestedCandidates
            .Select(candidate => candidate.TreeId)
            .ToArray();
        if (treeIds.Any(string.IsNullOrWhiteSpace) ||
            treeIds.Distinct(StringComparer.Ordinal).Count() != treeIds.Length ||
            treeIds.Length != storedByTreeId.Count ||
            treeIds.Any(treeId => !storedByTreeId.ContainsKey(treeId!)))
        {
            return new MissionPlantGpsPublishResult(
                MissionPlantGpsPublishStatus.Conflict,
                Errors: ["Payload candidates must match every saved CSV tree_id exactly once."]);
        }

        var enrichedZones = payload.Zones.Select(zone => zone with
        {
            Candidates = zone.Candidates.Select(candidate =>
            {
                var row = storedByTreeId[candidate.TreeId!];
                return candidate with
                {
                    Longitude = (double)row.Longitude,
                    Latitude = (double)row.Latitude,
                    ProposedRowIndex = row.Row,
                    ProposedColumnIndex = row.Col,
                    PositionConfidence = row.Confidence,
                    TreeId = row.TreeId,
                    Status = row.Status,
                    Source = row.Source
                };
            }).ToArray()
        }).ToArray();
        var enrichedPayload = payload with { Zones = enrichedZones };

        var validationErrors = V3BusinessContractValidators.Validate(enrichedPayload);
        if (validationErrors.Count != 0)
        {
            return new MissionPlantGpsPublishResult(
                MissionPlantGpsPublishStatus.Invalid,
                Errors: validationErrors);
        }

        var messagePayload = new BaselineMappingCandidatesReadyV3Message(
            enrichedPayload.SurveyOrderId,
            enrichedPayload.MissionId,
            enrichedPayload.FarmId,
            enrichedPayload.Zones.Select(zone =>
                new BaselineMappingZoneV3Message(
                    zone.ZoneId,
                    zone.RowSpacingM,
                    zone.PlantSpacingM,
                    zone.GridBearingDeg,
                    zone.Candidates.Select(candidate =>
                        new BaselineMappingCandidateV3Message(
                            candidate.Longitude,
                            candidate.Latitude,
                            candidate.ProposedRowIndex!.Value,
                            candidate.ProposedColumnIndex!.Value,
                            candidate.PositionConfidence,
                            candidate.TreeId!))
                    .ToArray()))
            .ToArray());

        var occurredAt = timeProvider.GetUtcNow();
        var envelope = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.BaselineMappingCandidatesReadyV3Message,
            idempotencyKey,
            correlationId,
            tenantId,
            actorId,
            occurredAt,
            messagePayload);
        db.OutboxMessages.Add(outboxMessageFactory.Create(
            envelope,
            IntegrationEventTypes.BaselineMappingCandidatesReadyV3,
            missionId.ToString("D")));
        await db.SaveChangesAsync(cancellationToken);

        return new MissionPlantGpsPublishResult(
            MissionPlantGpsPublishStatus.Queued, idempotencyKey);
    }
}
