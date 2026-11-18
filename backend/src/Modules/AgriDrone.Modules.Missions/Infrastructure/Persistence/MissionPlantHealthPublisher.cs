using System.Text.Json;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.Mapping;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Messaging.Validation;
using AgriDrone.Modules.Missions.Application.Abstractions.Missions;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Domain.Processing;
using AgriDrone.SharedInfrastructure.Messaging.Outbox;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Infrastructure.Persistence;

internal sealed class MissionPlantHealthPublisher(
    MissionsDbContext db, OutboxMessageFactory outboxMessageFactory, TimeProvider timeProvider) : IMissionPlantHealthPublisher
{
    private sealed record BoundaryRow(bool Covered, double Distance);

    public async Task<MissionPlantHealthPublishResult> PublishAsync(Guid tenantId, Guid farmId, Guid missionId,
        Guid actorId, Guid correlationId, Guid idempotencyKey, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var mission = await db.DroneMissions.FromSqlInterpolated($"SELECT *, xmin FROM mission.drone_missions WHERE id = {missionId} FOR UPDATE").SingleOrDefaultAsync(
            m => m.Id == missionId && m.FarmId == farmId && m.TenantId == tenantId, cancellationToken);
        if (mission is null) return new(MissionPlantHealthPublishStatus.NotFound);
        if (mission.Purpose != MissionPurpose.PlantHealth || mission.SurveyOrderId is not Guid orderId)
            return Conflict("A Plant Health Mission bound to an Order is required.");
        var existing = await db.OutboxMessages.AsNoTracking().SingleOrDefaultAsync(m => m.MessageId == idempotencyKey, cancellationToken);
        if (existing is not null)
            return existing.EventType == IntegrationEventTypes.PlantHealthAnalysisReadyV4 && existing.TenantId == tenantId && existing.PartitionKey == missionId.ToString("D")
                ? new(MissionPlantHealthPublishStatus.Queued, idempotencyKey) : Conflict("Idempotency-Key belongs to another operation/context.");
        var job = await db.AiProcessingJobs.AsNoTracking().Where(j => j.MissionId == missionId &&
                j.JobType == AiJobType.DiseaseDetection && j.Status == AiJobStatus.Completed)
            .OrderByDescending(j => j.AttemptNumber).ThenByDescending(j => j.QueuedAt).FirstOrDefaultAsync(cancellationToken);
        if (job is null) return Conflict("Run disease detection with recorded processing provenance before publishing.");
        PlantHealthProcessingSnapshot? snapshot;
        try { snapshot = job.Parameters.Deserialize<PlantHealthProcessingSnapshot>(); }
        catch (JsonException) { snapshot = null; }
        if (snapshot is null || snapshot.SourceVersion <= 0 || snapshot.Provenance is null)
            return Conflict("This legacy job has no Plant Health source snapshot. Reprocess with configured model provenance.");
        var observations = await db.MissionPlantObservations.AsNoTracking()
            .Where(o => o.MissionId == missionId && o.FarmId == farmId && o.AiJobId == job.Id).OrderBy(o => o.Id).ToListAsync(cancellationToken);
        List<PlantHealthFindingV4> findings = [];
        List<HealthBoundaryExceptionV4> exceptions = [];
        foreach (var observation in observations)
        {
            var metadata = observation.MatchingParameters.RootElement;
            if (observation.DetectedLocation is not { } position || observation.DetectionConfidence is not decimal confidence ||
                !metadata.TryGetProperty("source_finding_id", out var id) || !id.TryGetGuid(out var findingId) ||
                !metadata.TryGetProperty("class_name", out var label) || label.GetString() is not { Length: > 0 } className ||
                !metadata.TryGetProperty("observed_at", out var observed) || !observed.TryGetDateTimeOffset(out var observedAt))
                return Conflict("Observation source identity/location/class/confidence/time is incomplete; reprocess it.");
            var point = new GeoJsonPointV3("Point", [position.X, position.Y]);
            var boundary = await db.Database.SqlQuery<BoundaryRow>($"""
                SELECT ST_Covers(geometry, {position}) AS "Covered",
                    ST_Distance(ST_Boundary(geometry)::geography, {position}::geography) AS "Distance"
                FROM farm.farm_boundaries WHERE id = {snapshot.FarmBoundaryVersionId} AND farm_id = {farmId} AND tenant_id = {tenantId}
                """).SingleOrDefaultAsync(cancellationToken);
            if (boundary is null) return Conflict("The capture boundary is unavailable.");
            var classCode = ClassCode(className);
            IReadOnlyList<EvidenceMediaReferenceV4> media = observation.EvidenceMediaId is Guid mediaId
                ? [new(mediaId, "source_image")] : [];
            var uncertain = observation.ResolvedPlantId is null || classCode is null || !boundary.Covered || boundary.Distance <= (double)PlantHealthV4Values.BoundaryReviewThresholdMeters;
            findings.Add(new(findingId, snapshot.SourceVersion, observation.ResolvedPlantId, null,
                point, observedAt.ToUniversalTime(), classCode ?? className, PlantHealthV4Values.ClassificationNature,
                confidence, uncertain ? "REQUIRES_REVIEW" : "NONE", ["DRONE_AERIAL_VIEW_ONLY"], media));
            if (!boundary.Covered || boundary.Distance <= (double)PlantHealthV4Values.BoundaryReviewThresholdMeters)
                exceptions.Add(new(findingId.ToString("D"), point, boundary.Covered ? "NEEDS_REVIEW" : "OUT_OF_BOUNDARY",
                    (decimal)boundary.Distance, PlantHealthV4Values.BoundaryReviewThresholdMeters, media));
        }
        var now = timeProvider.GetUtcNow();
        // Zone proposals are optional; do not manufacture guidance references from clustered labels.
        var payload = new PlantHealthAnalysisReadyV4(correlationId, idempotencyKey, orderId, missionId, farmId,
            snapshot.FarmBoundaryVersionId, snapshot.FarmBaseMapVersionId, job.Id, now,
            PlantHealthV4Values.ResultNature, snapshot.TeamLabelLimitation, snapshot.Provenance, findings, exceptions, []);
        var errors = V4BusinessContractValidators.Validate(payload);
        if (errors.Count != 0) return Conflict(string.Join("; ", errors));
        var envelope = IntegrationEventEnvelopeFactory.Create(IntegrationEventDescriptors.PlantHealthAnalysisReadyV4,
            idempotencyKey, correlationId, tenantId, actorId, now, payload);
        db.OutboxMessages.Add(outboxMessageFactory.Create(envelope, IntegrationEventTypes.PlantHealthAnalysisReadyV4, missionId.ToString("D")));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(MissionPlantHealthPublishStatus.Queued, idempotencyKey);
    }

    private static MissionPlantHealthPublishResult Conflict(string error) => new(MissionPlantHealthPublishStatus.Conflict, Errors: [error]);
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
}
