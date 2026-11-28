using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Messaging.Validation;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Domain.Processing;
using AgriDrone.SharedInfrastructure.Messaging.Outbox;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Infrastructure.Persistence;

internal sealed class MissionComboAnalysisPublisher(
    MissionsDbContext db,
    ISurveyOrderCommercialSnapshotQuery commercialSnapshotQuery,
    OutboxMessageFactory outboxMessageFactory,
    IIntegrationMessageSerializer serializer,
    TimeProvider timeProvider) : IMissionComboAnalysisPublisher
{
    private static readonly Guid ComboAnalysisNamespace = new("e2c56a81-79b8-4e1b-93f4-3d0cf3b12345");

    private sealed record BoundaryRow(bool Covered, double Distance);
    private sealed record PlantProfileLookupRow(Guid PlantId, Guid PlantProfileId);
    private sealed record ImportedLineageRow(Guid JobId, int SourceRevision, Guid HandoffId, Guid? SupersedesHandoffId);
    internal sealed record SnapshotPoleRow(
        Guid MappingCandidateId,
        Guid SourceCandidateId,
        Guid PlantProfileId,
        Guid? TargetPlantProfileId,
        Guid? PlantId);

    internal static Guid? TryReadOptionalGuid(JsonElement metadata, string propertyName) =>
        metadata.ValueKind == JsonValueKind.Object &&
        metadata.TryGetProperty(propertyName, out var elem) &&
        elem.ValueKind == JsonValueKind.String &&
        elem.TryGetGuid(out var parsed) &&
        parsed != Guid.Empty
            ? parsed
            : null;

    internal static (Guid? PlantProfileId, Guid? MappingCandidateId) ResolveFindingIdentity(
        Guid? suggestedPlantId,
        Guid? resolvedPlantId,
        Guid? metadataProfileId,
        Guid? metadataCandidateId,
        IReadOnlyDictionary<Guid, SnapshotPoleRow> polesByPlantId,
        IReadOnlyDictionary<Guid, SnapshotPoleRow> polesBySourceCandidateId,
        IReadOnlyDictionary<Guid, SnapshotPoleRow> polesByCandidateRecordId,
        IReadOnlyDictionary<Guid, SnapshotPoleRow> polesByProfileId,
        IReadOnlyDictionary<Guid, Guid> plantIdToProfileMap)
    {
        var matchedPlantId = resolvedPlantId ?? suggestedPlantId;
        if (!matchedPlantId.HasValue && !metadataCandidateId.HasValue)
        {
            return (null, null);
        }

        SnapshotPoleRow? matchedPole = null;
        if (matchedPlantId is Guid plantId && polesByPlantId.TryGetValue(plantId, out var poleByPlant))
        {
            matchedPole = poleByPlant;
        }
        else if (metadataCandidateId is Guid candidateId &&
                 (polesBySourceCandidateId.TryGetValue(candidateId, out var poleBySource) ||
                  polesByCandidateRecordId.TryGetValue(candidateId, out poleBySource)))
        {
            matchedPole = poleBySource;
        }
        else if (metadataProfileId is Guid profileId && polesByProfileId.TryGetValue(profileId, out var poleByProfile))
        {
            matchedPole = poleByProfile;
        }

        if (matchedPole is not null)
        {
            return (matchedPole.PlantProfileId, matchedPole.SourceCandidateId);
        }

        if (resolvedPlantId is Guid resId && plantIdToProfileMap.TryGetValue(resId, out var resolvedProfileId))
        {
            return polesByProfileId.TryGetValue(resolvedProfileId, out var poleForResolved)
                ? (resolvedProfileId, poleForResolved.SourceCandidateId)
                : (resolvedProfileId, null);
        }

        return matchedPlantId.HasValue
            ? (metadataProfileId, metadataCandidateId)
            : (null, null);
    }

    public async Task<MissionComboAnalysisPublishResult> PublishAsync(
        Guid tenantId,
        Guid farmId,
        Guid missionId,
        Guid jobId,
        int sourceRevision,
        Guid actorId,
        Guid correlationId,
        Guid idempotencyKey,
        CancellationToken cancellationToken)
    {
        if (sourceRevision < 1)
            return Conflict("SourceRevision must be at least 1.");

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var mission = await db.DroneMissions
            .FromSqlInterpolated($"SELECT *, xmin FROM mission.drone_missions WHERE id = {missionId} FOR UPDATE")
            .SingleOrDefaultAsync(m => m.Id == missionId && m.FarmId == farmId && m.TenantId == tenantId, cancellationToken);
        if (mission is null)
            return new(MissionComboAnalysisPublishStatus.NotFound);

        if (mission.Purpose != MissionPurpose.Capture)
            return Conflict("Mission must have purpose Capture for COMBO analysis.");

        if (mission.SurveyOrderId is not Guid orderId)
            return Conflict("A Mission bound to an Order is required.");

        var commercial = await commercialSnapshotQuery.GetByOrderIdAsync(orderId, cancellationToken);
        if (commercial is null)
            return Conflict("The commercial snapshot for the bound Order is unavailable.");

        if (commercial.ServiceCode != "COMBO" || !commercial.ScreensDisease)
            return Conflict("Order commercial policy must be COMBO with ScreensDisease enabled.");

        var existing = await db.OutboxMessages.AsNoTracking()
            .SingleOrDefaultAsync(m => m.MessageId == idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.EventType != IntegrationEventTypes.ComboAnalysisReadyV1 ||
                existing.TenantId != tenantId ||
                existing.PartitionKey != missionId.ToString("D") ||
                existing.SchemaVersion != 1)
            {
                return Conflict("Idempotency-Key belongs to another operation/context.");
            }

            if (existing.ActorId != actorId)
            {
                return Conflict("Idempotency-Key belongs to another actor.");
            }

            if (existing.Body is null || existing.Body.Length == 0)
            {
                return Conflict("Corrupted outbox message body.");
            }

            IntegrationEventEnvelope<ComboAnalysisReadyV1>? existingEnvelope;
            try
            {
                existingEnvelope = serializer.Deserialize<ComboAnalysisReadyV1>(existing.Body);
            }
            catch (Exception ex)
            {
                return Conflict($"Failed to deserialize existing outbox payload: {ex.Message}");
            }

            if (existingEnvelope?.Payload is null)
            {
                return Conflict("Existing outbox payload is invalid or empty.");
            }

            var ep = existingEnvelope.Payload;
            if (ep.MissionId != missionId || ep.FarmId != farmId || ep.SurveyOrderId != orderId)
            {
                return Conflict("Idempotency-Key belongs to another mission/farm/order context.");
            }

            if (ep.JobId != jobId)
            {
                return Conflict("Idempotency-Key was already used for a different AI job.");
            }

            if (ep.SourceRevision != sourceRevision)
            {
                return Conflict("Idempotency-Key was already used for a different source revision.");
            }

            return new(MissionComboAnalysisPublishStatus.Queued, HandoffId: ep.HandoffId, MessageId: idempotencyKey);
        }

        var job = await db.AiProcessingJobs.AsNoTracking()
            .SingleOrDefaultAsync(j => j.Id == jobId && j.MissionId == missionId, cancellationToken);
        if (job is null)
            return Conflict($"AI processing job {jobId} was not found on mission {missionId}.");

        if (job.Status != AiJobStatus.Completed)
            return Conflict($"AI processing job {jobId} is not in completed state (current: {job.Status}).");

        ComboAnalysisProcessingSnapshot? comboSnapshot;
        try
        {
            comboSnapshot = job.Parameters.Deserialize<ComboAnalysisProcessingSnapshot>();
        }
        catch (JsonException)
        {
            comboSnapshot = null;
        }

        if (comboSnapshot is null)
        {
            return Conflict("AI processing job does not contain a valid COMBO snapshot.");
        }

        if (!job.HasCompleteComboInputCoverage())
            return Conflict("AI processing job has no complete COMBO input coverage proof.");

        if (comboSnapshot.SourceRevision != sourceRevision)
        {
            return Conflict($"Requested sourceRevision {sourceRevision} does not match persisted snapshot revision {comboSnapshot.SourceRevision}.");
        }

        var attemptId = comboSnapshot.CaptureAttemptId;
        if (attemptId == Guid.Empty || mission.CaptureAttemptId != attemptId)
            return Conflict("The COMBO snapshot must reference the mission's capture attempt.");

        var approvalReference = comboSnapshot.InputApprovalId?.ToString("D");
        var manifestReference = comboSnapshot.InputManifestId?.ToString("D");
        var approvedInputIsCurrent = await db.Database.SqlQuery<bool>($"""
            SELECT EXISTS (SELECT 1 FROM survey.capture_attempts
                WHERE id = {attemptId} AND order_id = {orderId} AND tenant_id = {tenantId} AND farm_id = {farmId}
                  AND input_approval_reference = {approvalReference} AND upload_manifest_reference = {manifestReference}
                  AND upload_manifest_fingerprint = {comboSnapshot.InputManifestHash}) AS "Value"
            """).SingleAsync(cancellationToken);
        if (!approvedInputIsCurrent)
            return Conflict("The approved input has changed. Run disease detection for the current approved input.");

        var lineage = await db.Database.SqlQuery<ImportedLineageRow>($"""
            SELECT batch.job_id AS "JobId", batch.source_revision AS "SourceRevision",
                   batch.handoff_id AS "HandoffId", batch.supersedes_handoff_id AS "SupersedesHandoffId"
            FROM survey.survey_combo_import_batches AS batch
            WHERE batch.tenant_id = {tenantId} AND batch.farm_id = {farmId}
              AND batch.survey_order_id = {orderId} AND batch.capture_attempt_id = {attemptId}
              AND batch.mission_id = {missionId}
              AND ((batch.job_id = {jobId} AND batch.source_revision = {sourceRevision}) OR NOT EXISTS (
                  SELECT 1
                  FROM survey.survey_combo_import_batches AS successor
                  WHERE successor.predecessor_batch_id = batch.id))
            ORDER BY (batch.job_id = {jobId} AND batch.source_revision = {sourceRevision}) DESC
            LIMIT 2
            """).ToListAsync(cancellationToken);

        var importedOutput = lineage.SingleOrDefault(batch => batch.JobId == jobId && batch.SourceRevision == sourceRevision);
        // Preserve the original lineage when an already imported output is sent with a new key.
        var head = importedOutput is null ? lineage.FirstOrDefault() : null;
        if (importedOutput is null && lineage.Count > 1)
            return Conflict("More than one imported COMBO batch is at the head of this capture attempt.");
        if (head is not null && head.SourceRevision >= sourceRevision)
            return Conflict("The requested source revision must be newer than the latest imported COMBO batch.");

        var boundaryId = comboSnapshot.FarmBoundaryVersionId;
        if (boundaryId == Guid.Empty)
            return Conflict("A valid FarmBoundaryVersionId in COMBO snapshot is required.");

        var baseMapId = comboSnapshot.FarmBaseMapVersionId;
        if (baseMapId == Guid.Empty)
            return Conflict("A valid FarmBaseMapVersionId in COMBO snapshot is required.");

        var baselineMapId = comboSnapshot.BaselineMapVersionId;
        if (baselineMapId == Guid.Empty)
            return Conflict("A valid BaselineMapVersionId in COMBO snapshot is required.");

        var mappingBatchId = comboSnapshot.MappingCandidateBatchId;
        if (mappingBatchId == Guid.Empty)
            return Conflict("A valid MappingCandidateBatchId in COMBO snapshot is required.");

        var mappingKind = comboSnapshot.MappingKind;
        var policyVersionId = comboSnapshot.BusinessPolicyVersionId;
        var limitationNotice = comboSnapshot.LimitationNotice;
        var provenance = comboSnapshot.Provenance;

        if (provenance is null)
        {
            return Conflict("COMBO provenance metadata is missing on the AI processing job.");
        }

        if (comboSnapshot.DiseaseZones is null)
        {
            return Conflict("DiseaseZones collection is missing or not confirmed in COMBO snapshot.");
        }

        var observations = await db.MissionPlantObservations.AsNoTracking()
            .Where(o => o.MissionId == missionId && o.FarmId == farmId && o.AiJobId == job.Id)
            .OrderBy(o => o.Id)
            .ToListAsync(cancellationToken);

        var snapshotPoles = await db.Database.SqlQuery<SnapshotPoleRow>($"""
            SELECT DISTINCT ON (i.id)
                i.mapping_candidate_id AS "MappingCandidateId",
                i.source_candidate_id AS "SourceCandidateId",
                i.plant_profile_id AS "PlantProfileId",
                i.target_plant_profile_id AS "TargetPlantProfileId",
                COALESCE(i.target_plant_id, p.id) AS "PlantId"
            FROM survey.survey_orders o
            JOIN survey.survey_order_pole_count_snapshot_items i
              ON i.snapshot_id = o.verified_pole_count_snapshot_id
             AND i.mapping_candidate_batch_id = {mappingBatchId}
             AND i.tenant_id = {tenantId}
             AND i.farm_id = {farmId}
             AND i.is_accepted = TRUE
             AND i.plant_profile_id IS NOT NULL
            LEFT JOIN plant.plants p
              ON p.farm_id = i.farm_id
             AND p.lifecycle_status = 'ACTIVE'
             AND (p.id = i.target_plant_id OR p.plant_profile_id = i.plant_profile_id)
            WHERE o.id = {orderId} AND o.tenant_id = {tenantId} AND o.farm_id = {farmId}
            ORDER BY i.id, (p.id = i.target_plant_id) DESC NULLS LAST, p.created_at DESC NULLS LAST, p.id
            """).ToListAsync(cancellationToken);

        var polesBySourceCandidateId = new Dictionary<Guid, SnapshotPoleRow>();
        var polesByCandidateRecordId = new Dictionary<Guid, SnapshotPoleRow>();
        var polesByPlantId = new Dictionary<Guid, SnapshotPoleRow>();
        var polesByProfileId = new Dictionary<Guid, SnapshotPoleRow>();
        foreach (var pole in snapshotPoles)
        {
            polesBySourceCandidateId.TryAdd(pole.SourceCandidateId, pole);
            polesByCandidateRecordId.TryAdd(pole.MappingCandidateId, pole);
            polesByProfileId.TryAdd(pole.PlantProfileId, pole);
            if (pole.TargetPlantProfileId.HasValue)
            {
                polesByProfileId.TryAdd(pole.TargetPlantProfileId.Value, pole);
            }
            if (pole.PlantId.HasValue)
            {
                polesByPlantId.TryAdd(pole.PlantId.Value, pole);
            }
        }

        var resolvedPlantIds = observations
            .Where(o => o.ResolvedPlantId.HasValue)
            .Select(o => o.ResolvedPlantId!.Value)
            .Distinct()
            .ToArray();

        Dictionary<Guid, Guid> plantIdToProfileMap = [];
        if (resolvedPlantIds.Length > 0)
        {
            var rows = await db.Database.SqlQuery<PlantProfileLookupRow>($"""
                SELECT id AS "PlantId", plant_profile_id AS "PlantProfileId"
                FROM plant.plants
                WHERE id = ANY({resolvedPlantIds}) AND farm_id = {farmId} AND plant_profile_id IS NOT NULL
                """).ToListAsync(cancellationToken);
            plantIdToProfileMap = rows.ToDictionary(r => r.PlantId, r => r.PlantProfileId);
        }

        List<ComboFindingV1> findings = [];
        List<ComboBoundaryExceptionV1> exceptions = [];
        foreach (var observation in observations)
        {
            var metadata = observation.MatchingParameters.RootElement;
            if (observation.DetectedLocation is not { } position ||
                observation.DetectionConfidence is not decimal confidence ||
                metadata.ValueKind != JsonValueKind.Object ||
                !metadata.TryGetProperty("source_finding_id", out var id) || id.ValueKind != JsonValueKind.String || !id.TryGetGuid(out var findingId) ||
                !metadata.TryGetProperty("class_name", out var label) || label.ValueKind != JsonValueKind.String || label.GetString() is not { Length: > 0 } className ||
                !metadata.TryGetProperty("observed_at", out var observed) || observed.ValueKind != JsonValueKind.String || !observed.TryGetDateTimeOffset(out var observedAt))
            {
                return Conflict("Observation source identity/location/class/confidence/time is incomplete; reprocess it.");
            }

            var point = new GeoJsonPointV3("Point", [position.X, position.Y]);
            var boundary = await db.Database.SqlQuery<BoundaryRow>($"""
                SELECT ST_Covers(geometry, {position}) AS "Covered",
                    ST_Distance(ST_Boundary(geometry)::geography, {position}::geography) AS "Distance"
                FROM farm.farm_boundaries WHERE id = {boundaryId} AND farm_id = {farmId} AND tenant_id = {tenantId}
                """).SingleOrDefaultAsync(cancellationToken);
            if (boundary is null) return Conflict("The capture boundary is unavailable.");

            var classCode = ClassCode(className) ?? className;
            var media = MissionPlantHealthPublisher.EvidenceMedia(observation.EvidenceMediaId, metadata);

            var metadataProfileId = TryReadOptionalGuid(metadata, "plant_profile_id");
            var metadataCandidateId = TryReadOptionalGuid(metadata, "mapping_candidate_id");

            // Only observations matched within threshold (or manually resolved) may carry pole identity.
            var (plantProfileId, mappingCandidateId) = ResolveFindingIdentity(
                observation.SuggestedPlantId,
                observation.ResolvedPlantId,
                metadataProfileId,
                metadataCandidateId,
                polesByPlantId,
                polesBySourceCandidateId,
                polesByCandidateRecordId,
                polesByProfileId,
                plantIdToProfileMap);

            var uncertain = (plantProfileId is null && mappingCandidateId is null) ||
                            !boundary.Covered ||
                            boundary.Distance <= (double)ComboAnalysisV1Values.BoundaryReviewThresholdMeters;

            findings.Add(new(
                findingId,
                sourceRevision,
                plantProfileId,
                mappingCandidateId,
                point,
                observedAt.ToUniversalTime(),
                classCode,
                ComboAnalysisV1Values.ClassificationNature,
                confidence,
                uncertain ? "REQUIRES_REVIEW" : "NONE",
                ["DRONE_AERIAL_VIEW_ONLY"],
                media));

            if (!boundary.Covered || boundary.Distance <= (double)ComboAnalysisV1Values.BoundaryReviewThresholdMeters)
            {
                exceptions.Add(new(
                    findingId.ToString("D"),
                    point,
                    boundary.Covered ? "NEEDS_REVIEW" : "OUT_OF_BOUNDARY",
                    (decimal)boundary.Distance,
                    ComboAnalysisV1Values.BoundaryReviewThresholdMeters,
                    media));
            }
        }

        var findingsById = findings.ToDictionary(f => f.FindingId);
        var diseaseZones = comboSnapshot.DiseaseZones
            .Select(z => z with
            {
                Memberships = z.Memberships
                    .Select(m => findingsById.TryGetValue(m.FindingId, out var resolvedFinding)
                        ? m with
                        {
                            PlantProfileId = resolvedFinding.PlantProfileId ?? m.PlantProfileId,
                            MappingCandidateId = resolvedFinding.MappingCandidateId ?? m.MappingCandidateId
                        }
                        : m)
                    .ToList()
            })
            .ToList();

        var handoffId = importedOutput?.HandoffId ?? CreateVersion5(
            ComboAnalysisNamespace,
            $"{tenantId:D}:{orderId:D}:{attemptId:D}:{job.Id:D}:{sourceRevision}");
        var now = timeProvider.GetUtcNow();
        var producedAt = (job.CompletedAt ?? now).ToUniversalTime();

        var payload = new ComboAnalysisReadyV1(
            CausationId: correlationId,
            HandoffId: handoffId,
            SurveyOrderId: orderId,
            FarmId: farmId,
            CaptureAttemptId: attemptId,
            MissionId: missionId,
            JobId: job.Id,
            SourceRevision: sourceRevision,
            ProducedAt: producedAt,
            SupersedesHandoffId: importedOutput?.SupersedesHandoffId ?? head?.HandoffId,
            BusinessPolicyVersionId: policyVersionId,
            InputApprovalId: comboSnapshot.InputApprovalId,
            InputManifestId: comboSnapshot.InputManifestId,
            InputManifestHash: comboSnapshot.InputManifestHash,
            MappingKind: mappingKind,
            MappingCandidateBatchId: mappingBatchId,
            FarmBaseMapVersionId: baseMapId,
            BaselineMapVersionId: baselineMapId,
            FarmBoundaryVersionId: boundaryId,
            ResultNature: ComboAnalysisV1Values.ResultNature,
            LimitationNotice: limitationNotice,
            Provenance: provenance,
            Findings: findings,
            BoundaryExceptions: exceptions,
            DiseaseZones: diseaseZones);

        var errors = ComboAnalysisReadyV1Validator.Validate(payload);
        if (errors.Count != 0)
            return new(MissionComboAnalysisPublishStatus.Invalid, Errors: errors);

        var envelope = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.ComboAnalysisReadyV1,
            idempotencyKey,
            correlationId,
            tenantId,
            actorId,
            now,
            payload);

        db.OutboxMessages.Add(outboxMessageFactory.Create(
            envelope,
            IntegrationEventTypes.ComboAnalysisReadyV1,
            missionId.ToString("D")));

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(MissionComboAnalysisPublishStatus.Queued, HandoffId: handoffId, MessageId: idempotencyKey);
    }

    private static MissionComboAnalysisPublishResult Conflict(string error) =>
        new(MissionComboAnalysisPublishStatus.Conflict, Errors: [error]);

    private static string? ClassCode(string label)
    {
        var normalized = label.Trim().Replace(' ', '_').ToUpperInvariant();
        return normalized switch
        {
            "SUNBURN" or "SUN_BURN" or "DISEASE_SUN_BURN" => "SUNBURN",
            "BROWN_SPOT" or "DISEASE_BROWN_SPOT" => "BROWN_SPOT",
            "ANTHRACNOSE" or "DISEASE_ANTHRACNOSE" => "ANTHRACNOSE",
            _ => null
        };
    }

    private static Guid CreateVersion5(Guid namespaceId, string name)
    {
        var namespaceBytes = namespaceId.ToByteArray();
        SwapGuidByteOrder(namespaceBytes);
        var nameBytes = Encoding.UTF8.GetBytes(name);
        var input = new byte[namespaceBytes.Length + nameBytes.Length];
        Buffer.BlockCopy(namespaceBytes, 0, input, 0, namespaceBytes.Length);
        Buffer.BlockCopy(nameBytes, 0, input, namespaceBytes.Length, nameBytes.Length);

#pragma warning disable CA5350 // RFC 4122 UUID v5 requires SHA-1; identity derivation
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
}
