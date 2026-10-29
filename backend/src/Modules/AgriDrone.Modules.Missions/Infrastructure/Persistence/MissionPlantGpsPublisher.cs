using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Messaging.Validation;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Domain.Missions;
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
        MissionPlantGpsPublishRequest request,
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

        var mission = await db.DroneMissions
            .Where(item => item.Id == missionId && item.FarmId == farmId &&
                           item.TenantId == tenantId)
            .SingleOrDefaultAsync(cancellationToken);
        if (mission is null)
        {
            return new MissionPlantGpsPublishResult(
                MissionPlantGpsPublishStatus.NotFound);
        }

        if (mission.SurveyOrderId is null ||
            mission.SurveyOrderId != request.SurveyOrderId ||
            mission.Purpose != MissionPurpose.BaselineMapping)
        {
            return new MissionPlantGpsPublishResult(
                MissionPlantGpsPublishStatus.Conflict,
                Errors: ["Payload survey order does not match a baseline mapping mission."]);
        }

        if (request.Candidates is null || request.Candidates.Count == 0)
        {
            return new MissionPlantGpsPublishResult(
                MissionPlantGpsPublishStatus.Invalid,
                Errors: ["Payload must contain complete candidate records."]);
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
        var candidatesRequest = request.Candidates.ToArray();
        var treeIds = candidatesRequest.Select(candidate => candidate.TreeId).ToArray();
        if (treeIds.Any(string.IsNullOrWhiteSpace) ||
            treeIds.Distinct(StringComparer.Ordinal).Count() != treeIds.Length ||
            treeIds.Length != storedByTreeId.Count ||
            treeIds.Any(treeId => !storedByTreeId.ContainsKey(treeId!)))
        {
            return new MissionPlantGpsPublishResult(
                MissionPlantGpsPublishStatus.Conflict,
                Errors: ["Payload candidates must match every saved CSV tree_id exactly once."]);
        }

        if (candidatesRequest.Any(candidate =>
                candidate.Row < 1 || candidate.Col < 1 ||
                candidate.Latitude is < -90 or > 90 ||
                candidate.Longitude is < -180 or > 180 ||
                candidate.Confidence is < 0 or > 1 ||
                string.IsNullOrWhiteSpace(candidate.Status) ||
                string.IsNullOrWhiteSpace(candidate.Source)))
        {
            return new MissionPlantGpsPublishResult(
                MissionPlantGpsPublishStatus.Invalid,
                Errors: ["Each candidate must include valid row, col, coordinates, confidence, status, and source."]);
        }

        if (candidatesRequest.Any(candidate =>
            {
                var stored = storedByTreeId[candidate.TreeId];
                return candidate.Row != stored.Row || candidate.Col != stored.Col ||
                       candidate.Latitude != stored.Latitude ||
                       candidate.Longitude != stored.Longitude ||
                       candidate.Confidence != stored.Confidence ||
                       !string.Equals(candidate.Status, stored.Status, StringComparison.Ordinal) ||
                       !string.Equals(candidate.Source, stored.Source, StringComparison.Ordinal);
            }))
        {
            return new MissionPlantGpsPublishResult(
                MissionPlantGpsPublishStatus.Conflict,
                Errors: ["Payload candidate fields must match the saved plant GPS rows."]);
        }

        var occurredAt = timeProvider.GetUtcNow();
        var candidates = candidatesRequest.Select(candidate =>
        {
            return new MappingCandidateV3(
                Longitude: (double)candidate.Longitude,
                Latitude: (double)candidate.Latitude,
                ProposedRowIndex: candidate.Row,
                ProposedColumnIndex: candidate.Col,
                PositionConfidence: candidate.Confidence,
                TreeId: candidate.TreeId,
                Status: candidate.Status,
                Source: candidate.Source);
        }).ToArray();
        var enrichedPayload = new BaselineMappingCandidatesReadyV3(
            request.SurveyOrderId,
            missionId,
            farmId,
            request.GridBearingDeg,
            request.RowSpacingM,
            request.PlantSpacingM,
            candidates);

        var validationErrors = V3BusinessContractValidators.Validate(enrichedPayload);
        if (validationErrors.Count != 0)
        {
            return new MissionPlantGpsPublishResult(
                MissionPlantGpsPublishStatus.Invalid,
                Errors: validationErrors);
        }

        var envelope = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.BaselineMappingCandidatesReadyV3,
            idempotencyKey,
            correlationId,
            tenantId,
            actorId,
            occurredAt,
            enrichedPayload);
        db.OutboxMessages.Add(outboxMessageFactory.Create(
            envelope,
            IntegrationEventTypes.BaselineMappingCandidatesReadyV3,
            missionId.ToString("D")));
        await db.SaveChangesAsync(cancellationToken);

        return new MissionPlantGpsPublishResult(
            MissionPlantGpsPublishStatus.Queued, idempotencyKey);
    }
}
