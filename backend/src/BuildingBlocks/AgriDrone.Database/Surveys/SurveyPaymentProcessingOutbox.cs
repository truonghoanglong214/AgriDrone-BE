using System.Text.Json;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.Modules.Plants.Domain.Health;
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
              AND r.farm_id = {order.FarmId}
              AND p.tenant_id = r.tenant_id AND p.farm_id = r.farm_id
              AND p.batch_id = r.approved_batch_id AND r.active_import_batch_id = r.approved_batch_id
              AND p.approval_fingerprint = r.approval_fingerprint
              AND r.approved_review_revision = r.review_revision
              AND r.status = 'PUBLISHED' AND r.invalidated_at IS NULL
            """).SingleOrDefaultAsync(cancellationToken);
        if (json is null)
            throw new InvalidOperationException("Final settlement requires the exact reviewed post-flight publication.");
        var publication = JsonSerializer.Deserialize<PlantHealthPreliminaryResultPublishedV1>(json)
            ?? throw new InvalidOperationException("Published result payload is missing.");
        // Advance the customer projection only in the successful settlement transaction.
        // Preserve a newer released observation when an older Order is paid later.
        foreach (var group in publication.Findings.GroupBy(finding => finding.PlantId).OrderBy(group => group.Key))
        {
            var latest = LatestReviewedPlantHealth.Create(order.TenantId, publication, group.ToArray());
            var findingsJson = latest.Findings.RootElement.GetRawText();
            await context.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO plant.latest_reviewed_plant_health AS current
                    (plant_id, tenant_id, farm_id, publication_id, survey_result_id, observed_at,
                     publication_version, result_nature, team_label_limitation, findings)
                VALUES ({latest.PlantId}, {latest.TenantId}, {latest.FarmId}, {latest.PublicationId},
                    {latest.SurveyResultId}, {latest.ObservedAt}, {latest.PublicationVersion},
                    {latest.ResultNature}, {latest.TeamLabelLimitation}, {findingsJson}::jsonb)
                ON CONFLICT (plant_id) DO UPDATE SET
                    publication_id = EXCLUDED.publication_id,
                    survey_result_id = EXCLUDED.survey_result_id,
                    observed_at = EXCLUDED.observed_at,
                    publication_version = EXCLUDED.publication_version,
                    result_nature = EXCLUDED.result_nature,
                    team_label_limitation = EXCLUDED.team_label_limitation,
                    findings = EXCLUDED.findings
                WHERE current.tenant_id = EXCLUDED.tenant_id AND current.farm_id = EXCLUDED.farm_id
                  AND (current.observed_at, current.publication_version, current.survey_result_id)
                    < (EXCLUDED.observed_at, EXCLUDED.publication_version, EXCLUDED.survey_result_id)
                """, cancellationToken);
        }
        var outcome = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.PlantHealthPreliminaryResultPublishedV1,
            Guid.CreateVersion7(), correlationId, order.TenantId, publication.PublishedBy,
            order.UpdatedAt, publication, causationId: publication.CausationId);
        Add(outcome, order.FarmId.ToString());
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
