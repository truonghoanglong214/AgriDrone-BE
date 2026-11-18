using System.Text.Json;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedInfrastructure.Messaging.Inbox;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Health;

internal sealed class PlantHealthAnalysisReadyV4Handler(
    SurveyResultPublicationDbContext db, InboxExecutionCoordinator coordinator, IPlantHealthPendingImport import)
    : IIntegrationMessageHandler<PlantHealthAnalysisReadyV4>
{
    public async Task<IntegrationMessageProcessingResult> HandleAsync(
        IntegrationEventEnvelope<PlantHealthAnalysisReadyV4> envelope, CancellationToken cancellationToken)
    {
        var fingerprint = PlantHealthV4Fingerprint.ForPayload(envelope.Payload);
        var result = await coordinator.ExecuteAsync(db, IntegrationConsumerNames.Be1PlantHealthAnalysisReadyV4, envelope,
            async (_, token) =>
            {
                var outcome = await import.ImportAsync(envelope, token);
                return outcome.Disposition switch
                {
                    PlantHealthImportDisposition.Completed => InboxHandlerResult.Completed(JsonSerializer.Serialize(new { Fingerprint = fingerprint, Outcome = outcome })),
                    PlantHealthImportDisposition.Retry => InboxHandlerResult.Retry(outcome.Error),
                    PlantHealthImportDisposition.Rejected => InboxHandlerResult.PermanentFailure(outcome.ErrorCode!, outcome.Error),
                    _ => throw new InvalidOperationException("Unknown pending import outcome.")
                };
            }, cancellationToken);
        if (result.Disposition != IntegrationMessageDisposition.Acknowledge) return result;
        // Check even the coordinator's duplicate/race path before the broker receives ACK.
        var stored = await db.InboxMessages.AsNoTracking().SingleAsync(m =>
            m.ConsumerName == IntegrationConsumerNames.Be1PlantHealthAnalysisReadyV4 && m.MessageId == envelope.MessageId, cancellationToken);
        using var original = JsonDocument.Parse(stored.Result!);
        return stored.TenantId == envelope.TenantId &&
            original.RootElement.GetProperty("Fingerprint").GetString() == fingerprint
            ? result : IntegrationMessageProcessingResult.DeadLetter("PlantHealthAnalysisReadyV4.IdentityPayloadConflict",
                "MessageId has already been used with another payload or Tenant.");
    }
}
