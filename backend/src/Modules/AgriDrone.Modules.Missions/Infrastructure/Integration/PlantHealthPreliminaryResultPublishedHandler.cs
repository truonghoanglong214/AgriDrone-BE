using System.Text.Json;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.Modules.Missions.Domain.Missions;
using AgriDrone.Modules.Missions.Infrastructure.Persistence;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedInfrastructure.Messaging.Inbox;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Modules.Missions.Infrastructure.Integration;

internal sealed class PlantHealthPreliminaryResultPublishedHandler(MissionsDbContext db,
    InboxExecutionCoordinator inbox, IPlantHealthPublicationQuery publications, IPlantHealthSourceContextQuery source)
    : IIntegrationMessageHandler<PlantHealthPreliminaryResultPublishedV1>
{
    public Task<IntegrationMessageProcessingResult> HandleAsync(IntegrationEventEnvelope<PlantHealthPreliminaryResultPublishedV1> envelope,
        CancellationToken token) => inbox.ExecuteAsync(db, IntegrationConsumerNames.Be2PlantHealthPreliminaryResultPublishedV1,
            envelope, (_, ct) => ApplyAsync(envelope, ct), token);

    private async Task<InboxHandlerResult> ApplyAsync(IntegrationEventEnvelope<PlantHealthPreliminaryResultPublishedV1> envelope, CancellationToken token)
    {
        var p = envelope.Payload;
        if (envelope.TenantId is not Guid tenant || envelope.ActorId != p.PublishedBy || envelope.CausationId != p.CausationId || envelope.OccurredAt != p.PublishedAt)
            return InboxHandlerResult.PermanentFailure("PlantHealthPublication.EnvelopeMismatch", "Publication actor, causation and time must match its envelope.");
        var mission = await db.DroneMissions.FromSqlInterpolated($"SELECT * FROM mission.drone_missions WHERE id = {p.MissionId} FOR UPDATE")
            .SingleOrDefaultAsync(token);
        if (mission is null) return InboxHandlerResult.Retry("The source Mission is not available yet.");
        if (mission.TenantId != tenant || mission.FarmId != p.FarmId || mission.SurveyOrderId != p.SurveyOrderId ||
            mission.Purpose != MissionPurpose.PlantHealth || mission.FarmBoundaryVersionId != p.FarmBoundaryVersionId || mission.SourceMapVersionId != p.FarmBaseMapVersionId)
            return InboxHandlerResult.PermanentFailure("PlantHealthPublication.MissionContextMismatch", "The outcome does not match the Mission capture context.");
        var proof = await publications.GetAsync(tenant, p.PublicationId, token);
        if (proof is null) return InboxHandlerResult.Retry("The committed publication reference is not available yet.");
        if (PlantHealthV4Fingerprint.ForValue(p) != proof.Fingerprint)
            return InboxHandlerResult.PermanentFailure("PlantHealthPublication.PayloadMismatch", "The outcome differs from the immutable published payload.");
        var job = await source.GetAsync(p.MissionId, p.JobId, token);
        if (job is null) return InboxHandlerResult.Retry("The source analysis job is not available yet.");
        if (!job.Completed || !job.IsPlantHealth || job.TenantId != tenant || job.FarmId != p.FarmId || job.SurveyOrderId != p.SurveyOrderId ||
            job.SourceVersion != p.SourceVersion || job.FarmBoundaryVersionId != p.FarmBoundaryVersionId || job.FarmBaseMapVersionId != p.FarmBaseMapVersionId ||
            job.Provenance != p.Provenance || job.TeamLabelLimitation != p.TeamLabelLimitation)
            return InboxHandlerResult.PermanentFailure("PlantHealthPublication.JobContextMismatch", "The published outcome does not match its completed source analysis.");
        bool changed;
        try
        {
            changed = mission.ApplyPublishedPlantHealth(p.PublicationId, p.SurveyResultId, p.PublicationVersion,
                proof.Fingerprint, p.HandoffId, p.ApprovedReviewRevision, proof.ReviewedFindingCount, p.PublishedAt);
        }
        catch (InvalidOperationException error)
        { return InboxHandlerResult.PermanentFailure("PlantHealthPublication.MissionStateInvalid", error.Message); }
        if (changed)
            db.AuditLogs.Add(AuditLog.ForIntegrationAction(tenant, p.FarmId, p.PublicationId, envelope.CorrelationId,
                "DroneMission", mission.Id, "PlantHealth.MissionCompletedAfterPublication", JsonSerializer.SerializeToDocument(new
                { p.PublicationId, p.SurveyResultId, p.PublicationVersion, proof.Fingerprint, proof.ReviewedFindingCount }), p.PublishedAt));
        return InboxHandlerResult.Completed(JsonSerializer.Serialize(new { p.PublicationId, MissionId = mission.Id, Changed = changed }));
    }
}
