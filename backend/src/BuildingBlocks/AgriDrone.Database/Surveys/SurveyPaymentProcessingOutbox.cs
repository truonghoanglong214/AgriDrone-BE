using System.Text.Json;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.Modules.Surveys.Domain;
using Microsoft.EntityFrameworkCore;
using AgriDrone.Modules.Surveys.Application.Abstractions.Payments;
using AgriDrone.SharedInfrastructure.Messaging.Outbox;

namespace AgriDrone.Database.Surveys;

internal sealed class SurveyPaymentProcessingOutbox(
    SurveyPaymentProcessingDbContext context,
    OutboxMessageFactory outboxMessageFactory)
    : ISurveyPaymentProcessingOutbox
{
    public async Task AddPostFlightUnlockAsync(SurveyOrder order, Guid correlationId, CancellationToken cancellationToken = default)
    {
        if (!order.PostFlightSettlement || order.Status != SurveyOrderStatus.Completed) return;
        context.EnsureTransaction();
        var json = await context.Database.SqlQuery<string>($"""
            SELECT p.payload::text AS "Value"
            FROM survey.plant_health_publications p
            JOIN survey.survey_results r ON r.id = p.survey_result_id
            WHERE r.survey_order_id = {order.Id} AND r.tenant_id = {order.TenantId}
              AND r.status = 'PUBLISHED' AND r.invalidated_at IS NULL
            """).SingleOrDefaultAsync(cancellationToken);
        // Mapping-only/manual fixtures may not have a Plant Health publication.
        if (json is null) return;
        var publication = JsonSerializer.Deserialize<PlantHealthPreliminaryResultPublishedV1>(json)
            ?? throw new InvalidOperationException("Published result payload is missing.");
        var intent = new PlantHealthPublicationNotificationRequestedV1(publication.CausationId, publication.PublicationId,
            order.Id, publication.SurveyResultId, order.FarmId, publication.PublicationVersion,
            publication.ResultNature, publication.TeamLabelLimitation, publication.PublishedAt);
        var envelope = IntegrationEventEnvelopeFactory.Create(IntegrationEventDescriptors.PlantHealthPublicationNotificationRequestedV1,
            Guid.CreateVersion7(), correlationId, order.TenantId, publication.PublishedBy, publication.PublishedAt,
            intent, causationId: publication.CausationId);
        Add(envelope, order.FarmId.ToString());
    }

    public void Add<TPayload>(
        IntegrationEventEnvelope<TPayload> envelope,
        string? partitionKey = null)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        context.AddOutboxMessage(outboxMessageFactory.Create(
            envelope,
            envelope.EventType,
            partitionKey));
    }
}
