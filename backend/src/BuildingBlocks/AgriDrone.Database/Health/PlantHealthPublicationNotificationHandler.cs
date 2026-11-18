using System.Text.Json;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.Modules.Notifications.Domain.Notifications;
using AgriDrone.SharedInfrastructure.Auditing;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedInfrastructure.Messaging.Inbox;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Health;

internal sealed class PlantHealthPublicationNotificationHandler(SurveyResultPublicationDbContext db,
    InboxExecutionCoordinator inbox, IPlantHealthPublicationQuery publications, TimeProvider clock)
    : IIntegrationMessageHandler<PlantHealthPublicationNotificationRequestedV1>
{
    private sealed record OwnerRow(Guid UserId);

    public Task<IntegrationMessageProcessingResult> HandleAsync(IntegrationEventEnvelope<PlantHealthPublicationNotificationRequestedV1> envelope,
        CancellationToken token) => inbox.ExecuteAsync(db, IntegrationConsumerNames.PlantHealthPublicationNotificationV1,
            envelope, (_, ct) => ApplyAsync(envelope, ct), token);

    private async Task<InboxHandlerResult> ApplyAsync(IntegrationEventEnvelope<PlantHealthPublicationNotificationRequestedV1> envelope, CancellationToken token)
    {
        var p = envelope.Payload;
        if (envelope.TenantId is not Guid tenant || envelope.CausationId != p.CausationId || envelope.OccurredAt != p.PublishedAt)
            return InboxHandlerResult.PermanentFailure("PlantHealthNotification.EnvelopeMismatch", "The notification intent must match its publication envelope.");
        var proof = await publications.GetAsync(tenant, p.PublicationId, token);
        if (proof is null) return InboxHandlerResult.Retry("The committed published reference is not available yet.");
        var published = proof.Payload;
        if (p.CausationId != published.CausationId || p.SurveyOrderId != published.SurveyOrderId || p.SurveyResultId != published.SurveyResultId ||
            p.FarmId != published.FarmId || p.PublicationVersion != published.PublicationVersion || p.ResultNature != published.ResultNature ||
            p.TeamLabelLimitation != published.TeamLabelLimitation || p.PublishedAt != published.PublishedAt || envelope.ActorId != published.PublishedBy)
            return InboxHandlerResult.PermanentFailure("PlantHealthNotification.PublicationMismatch", "The intent differs from its immutable published reference.");
        var notificationLock = $"plant-health-notification:{tenant:D}:{p.PublicationId:D}";
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({notificationLock}, 0))", token);
        var existing = await db.HealthPublicationNotifications.AsNoTracking().SingleOrDefaultAsync(n => n.SourcePublicationId == p.PublicationId, token);
        if (existing is not null) return InboxHandlerResult.Completed(JsonSerializer.Serialize(new { p.PublicationId, NotificationId = existing.Id, IsReplay = true }));
        var owner = await db.Database.SqlQuery<OwnerRow>($"""
            SELECT u.id AS "UserId" FROM identity.tenant_memberships tm
            JOIN identity.users u ON u.id = tm.user_id
            JOIN identity.tenants t ON t.id = tm.tenant_id
            JOIN farm.farms f ON f.tenant_id = t.id
            WHERE tm.tenant_id = {tenant} AND f.id = {p.FarmId} AND tm.role = 'OWNER' AND tm.status = 'ACTIVE'
              AND u.status = 'ACTIVE' AND u.deleted_at IS NULL AND t.status = 'ACTIVE' AND t.deleted_at IS NULL
              AND f.status = 'ACTIVE' AND f.deleted_at IS NULL
            FOR SHARE OF tm, u, t, f
            """).SingleOrDefaultAsync(token);
        if (owner is null) return InboxHandlerResult.Retry("No current active owner recipient is available; retry or redrive after reconciliation.");
        var now = clock.GetUtcNow();
        var notification = Notification.ForPublishedPlantHealth(owner.UserId, tenant, p.FarmId, p.PublicationId, p.TeamLabelLimitation, now);
        db.HealthPublicationNotifications.Add(notification);
        db.AddAuditLog(AuditLog.ForIntegrationAction(tenant, p.FarmId, p.PublicationId, envelope.CorrelationId,
            "Notification", notification.Id, "PlantHealth.PublishedOwnerNotificationCreated", JsonSerializer.SerializeToDocument(new
            { p.PublicationId, p.SurveyResultId, RecipientId = owner.UserId }), now));
        return InboxHandlerResult.Completed(JsonSerializer.Serialize(new { p.PublicationId, NotificationId = notification.Id }));
    }
}
