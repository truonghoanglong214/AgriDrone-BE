using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Messaging.Validation;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Domain.Processing;
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
            .SingleOrDefaultAsync(message => message.MessageId == idempotencyKey, cancellationToken);
        if (existingMessage is not null)
        {
            return existingMessage.EventType == IntegrationEventTypes.MappingCandidatesReadyV4
                ? new MissionPlantGpsPublishResult(MissionPlantGpsPublishStatus.Queued, idempotencyKey)
                : new MissionPlantGpsPublishResult(
                    MissionPlantGpsPublishStatus.Conflict,
                    Errors: ["Idempotency-Key has already been used for another event."]);
        }

        var mission = await db.DroneMissions
            .Where(item => item.Id == missionId && item.FarmId == farmId && item.TenantId == tenantId)
            .SingleOrDefaultAsync(cancellationToken);
        if (mission is null)
        {
            return new MissionPlantGpsPublishResult(MissionPlantGpsPublishStatus.NotFound);
        }

        if (mission.SurveyOrderId is null || mission.SurveyOrderId != request.SurveyOrderId)
        {
            return new MissionPlantGpsPublishResult(
                MissionPlantGpsPublishStatus.Conflict,
                Errors: ["Payload survey order does not match the mission context."]);
        }

        if (!mission.CaptureAttemptId.HasValue || mission.CaptureAttemptId.Value == Guid.Empty)
        {
            return new MissionPlantGpsPublishResult(
                MissionPlantGpsPublishStatus.Conflict,
                Errors: ["Mission must be associated with a valid CaptureAttempt before publishing mapping candidates."]);
        }

        var captureAttemptId = mission.CaptureAttemptId.Value;
        var captureAttemptMatches = await db.Database.SqlQueryRaw<int>(
                "SELECT 1 AS \"Value\" FROM survey.capture_attempts " +
                "WHERE id = {0} AND order_id = {1} AND mission_id = {2} " +
                "AND tenant_id = {3} AND farm_id = {4}",
                captureAttemptId,
                request.SurveyOrderId,
                missionId,
                tenantId,
                farmId)
            .AnyAsync(cancellationToken);
        if (!captureAttemptMatches)
        {
            return new MissionPlantGpsPublishResult(
                MissionPlantGpsPublishStatus.Conflict,
                Errors: ["Mission CaptureAttempt does not match the tenant, farm, order, and mission context."]);
        }

        if (!mission.FarmBoundaryVersionId.HasValue || mission.FarmBoundaryVersionId.Value == Guid.Empty)
        {
            return new MissionPlantGpsPublishResult(
                MissionPlantGpsPublishStatus.Conflict,
                Errors: ["Mission must be associated with a valid FarmBoundaryVersion before publishing mapping candidates."]);
        }

        var farmBoundaryVersionId = mission.FarmBoundaryVersionId.Value;

        // Whitelist mapping kind according to mission purpose
        MappingKind determinedKind;
        Guid? expectedCurrentMapId;
        if (mission.Purpose == MissionPurpose.BaselineMapping)
        {
            determinedKind = MappingKind.FirstBaseline;
            expectedCurrentMapId = null;
        }
        else if (mission.Purpose is MissionPurpose.PlantHealth or MissionPurpose.HarvestReadiness)
        {
            if (!mission.SourceMapVersionId.HasValue || mission.SourceMapVersionId.Value == Guid.Empty)
            {
                return new MissionPlantGpsPublishResult(
                    MissionPlantGpsPublishStatus.Conflict,
                    Errors: ["Follow-up mapping mission must have a pinned SourceMapVersionId."]);
            }

            determinedKind = MappingKind.FollowUpSnapshot;
            expectedCurrentMapId = mission.SourceMapVersionId;
        }
        else
        {
            return new MissionPlantGpsPublishResult(
                MissionPlantGpsPublishStatus.Conflict,
                Errors: [$"Mission purpose '{mission.Purpose}' is not eligible for mapping candidate publication."]);
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

        if (storedRows.Any(row => string.IsNullOrWhiteSpace(row.TreeId)) ||
            storedRows.Select(row => row.TreeId).Distinct(StringComparer.Ordinal).Count() != storedRows.Count)
        {
            return new MissionPlantGpsPublishResult(
                MissionPlantGpsPublishStatus.Conflict,
                Errors: ["Saved mapping output contains missing or duplicate tree_id values."]);
        }

        if (storedRows.Any(row =>
                row.Row < 1 || row.Col < 1 ||
                row.Latitude is < -90 or > 90 ||
                row.Longitude is < -180 or > 180 ||
                row.Confidence is < 0 or > 1 ||
                string.IsNullOrWhiteSpace(row.Status) ||
                string.IsNullOrWhiteSpace(row.Source)))
        {
            return new MissionPlantGpsPublishResult(
                MissionPlantGpsPublishStatus.Invalid,
                Errors: ["Saved mapping output contains invalid row, column, coordinates, confidence, status, or source values."]);
        }

        var processingJob = await db.AiProcessingJobs
            .Include(j => j.ModelVersion)
            .SingleOrDefaultAsync(j => j.Id == request.ProcessingJobId &&
                                       j.MissionId == missionId &&
                                       j.JobType == AiJobType.Mapping &&
                                       j.Status == AiJobStatus.Completed, cancellationToken);

        if (processingJob is null || !processingJob.ModelVersionId.HasValue || !processingJob.CompletedAt.HasValue)
        {
            return new MissionPlantGpsPublishResult(
                MissionPlantGpsPublishStatus.Conflict,
                Errors: ["A completed AI mapping processing job was not found for this mission. Complete mapping AI processing before publishing mapping candidates."]);
        }

        var handoffId = idempotencyKey;
        var jobId = processingJob.Id;
        var modelVersionId = processingJob.ModelVersionId.Value;
        var producedAt = processingJob.CompletedAt.Value;

        if (processingJob.ModelVersion is null)
        {
            return new MissionPlantGpsPublishResult(
                MissionPlantGpsPublishStatus.Conflict,
                Errors: ["The mapping processing job references a missing AI model version."]);
        }

        MappingProcessingSnapshot? processingSnapshot;
        try
        {
            processingSnapshot = processingJob.Parameters.Deserialize<MappingProcessingSnapshot>();
        }
        catch (JsonException)
        {
            processingSnapshot = null;
        }

        if (processingSnapshot is null ||
            processingSnapshot.SourceVersion < 1 ||
            processingJob.AttemptNumber != processingSnapshot.SourceVersion ||
            processingSnapshot.GridBearingDeg is < 0 or >= 360 ||
            processingSnapshot.RowSpacingM <= 0 ||
            processingSnapshot.PlantSpacingM <= 0 ||
            string.IsNullOrWhiteSpace(processingSnapshot.AlgorithmVersion) ||
            !IsSha256(processingSnapshot.InputFingerprint) ||
            !IsSha256(processingSnapshot.OutputFingerprint) ||
            !string.Equals(
                MissionPlantGpsFingerprint.Compute(storedRows),
                processingSnapshot.OutputFingerprint,
                StringComparison.Ordinal))
        {
            return new MissionPlantGpsPublishResult(
                MissionPlantGpsPublishStatus.Conflict,
                Errors: ["The mapping processing job does not contain a complete immutable mapping snapshot."]);
        }

        var candidates = storedRows.Select(candidate =>
        {
            var deterministicCandidateId = CreateVersion5(
                jobId,
                candidate.TreeId.Trim().ToUpperInvariant());
            return new MappingCandidateV3(
                Longitude: (double)candidate.Longitude,
                Latitude: (double)candidate.Latitude,
                ProposedRowIndex: candidate.Row,
                ProposedColumnIndex: candidate.Col,
                PositionConfidence: candidate.Confidence,
                TreeId: candidate.TreeId.Trim(),
                CandidateId: deterministicCandidateId,
                SourceObservationId: null,
                Status: candidate.Status,
                Source: candidate.Source);
        }).ToArray();

        var enrichedPayload = new MappingCandidatesReadyV4(
            MappingKind: determinedKind,
            SurveyOrderId: request.SurveyOrderId,
            MissionId: missionId,
            CaptureAttemptId: captureAttemptId,
            FarmId: farmId,
            FarmBoundaryVersionId: farmBoundaryVersionId,
            HandoffId: handoffId,
            JobId: jobId,
            ModelVersionId: modelVersionId,
            ExpectedCurrentFarmBaseMapVersionId: expectedCurrentMapId,
            ProducedAt: producedAt,
            GridBearingDeg: processingSnapshot.GridBearingDeg,
            RowSpacingM: processingSnapshot.RowSpacingM,
            PlantSpacingM: processingSnapshot.PlantSpacingM,
            Candidates: candidates,
            ModelVersion: processingJob.ModelVersion.Version,
            AlgorithmVersion: processingSnapshot.AlgorithmVersion,
            BoundaryPolicyVersion: null,
            CausationId: correlationId);

        var validationErrors = V3BusinessContractValidators.Validate(enrichedPayload);
        if (validationErrors.Count != 0)
        {
            return new MissionPlantGpsPublishResult(
                MissionPlantGpsPublishStatus.Invalid,
                Errors: validationErrors);
        }

        var envelope = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.MappingCandidatesReadyV4,
            idempotencyKey,
            correlationId,
            tenantId,
            actorId,
            timeProvider.GetUtcNow(),
            enrichedPayload);

        db.OutboxMessages.Add(outboxMessageFactory.Create(
            envelope,
            IntegrationEventTypes.MappingCandidatesReadyV4,
            missionId.ToString("D")));
        await db.SaveChangesAsync(cancellationToken);

        return new MissionPlantGpsPublishResult(
            MissionPlantGpsPublishStatus.Queued, idempotencyKey);
    }

    private static Guid CreateVersion5(Guid namespaceId, string name)
    {
        var namespaceBytes = namespaceId.ToByteArray();
        SwapGuidByteOrder(namespaceBytes);
        var nameBytes = Encoding.UTF8.GetBytes(name);
        var input = new byte[namespaceBytes.Length + nameBytes.Length];
        Buffer.BlockCopy(namespaceBytes, 0, input, 0, namespaceBytes.Length);
        Buffer.BlockCopy(nameBytes, 0, input, namespaceBytes.Length, nameBytes.Length);

#pragma warning disable CA5350 // RFC 4122 UUID v5 requires SHA-1; this is identity derivation, not security.
        var hash = SHA1.HashData(input);
#pragma warning restore CA5350
        var guidBytes = hash[..16];
        guidBytes[6] = (byte)((guidBytes[6] & 0x0F) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
        SwapGuidByteOrder(guidBytes);
        return new Guid(guidBytes);
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(Uri.IsHexDigit);

    private static void SwapGuidByteOrder(byte[] bytes)
    {
        (bytes[0], bytes[3]) = (bytes[3], bytes[0]);
        (bytes[1], bytes[2]) = (bytes[2], bytes[1]);
        (bytes[4], bytes[5]) = (bytes[5], bytes[4]);
        (bytes[6], bytes[7]) = (bytes[7], bytes[6]);
    }

}
