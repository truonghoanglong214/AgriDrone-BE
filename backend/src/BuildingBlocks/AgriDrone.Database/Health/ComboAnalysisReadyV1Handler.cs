using System.Text.Json;
using AgriDrone.IntegrationContracts.Health;
using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.SharedInfrastructure.Messaging.Consumers;
using AgriDrone.SharedInfrastructure.Messaging.Inbox;
using Microsoft.EntityFrameworkCore;

namespace AgriDrone.Database.Health;

internal sealed class ComboAnalysisReadyV1Handler(
    SurveyComboImportDbContext db,
    InboxExecutionCoordinator coordinator,
    IComboAnalysisPendingImport import)
    : IIntegrationMessageHandler<ComboAnalysisReadyV1>
{
    public async Task<IntegrationMessageProcessingResult> HandleAsync(
        IntegrationEventEnvelope<ComboAnalysisReadyV1> envelope, CancellationToken cancellationToken)
    {
        var fingerprint = ComboAnalysisV1Fingerprint.ForPayload(envelope.Payload);
        var result = await coordinator.ExecuteAsync(db, IntegrationConsumerNames.Be1ComboAnalysisReadyV1, envelope,
            async (_, token) =>
            {
                var outcome = await import.ImportAsync(envelope, token);
                return outcome.Disposition switch
                {
                    ComboAnalysisImportDisposition.Completed => InboxHandlerResult.Completed(
                        JsonSerializer.Serialize(new { Fingerprint = fingerprint, Outcome = outcome })),
                    ComboAnalysisImportDisposition.Retry => InboxHandlerResult.Retry(outcome.Error),
                    ComboAnalysisImportDisposition.Rejected => InboxHandlerResult.PermanentFailure(outcome.ErrorCode!, outcome.Error),
                    _ => throw new InvalidOperationException("Unknown pending import outcome.")
                };
            }, cancellationToken);

        if (result.Disposition != IntegrationMessageDisposition.Acknowledge)
            return result;

        var stored = await db.InboxMessages.AsNoTracking().SingleAsync(m =>
            m.ConsumerName == IntegrationConsumerNames.Be1ComboAnalysisReadyV1 && m.MessageId == envelope.MessageId, cancellationToken);
        using var original = JsonDocument.Parse(stored.Result!);
        return stored.TenantId == envelope.TenantId &&
               original.RootElement.GetProperty("Fingerprint").GetString() == fingerprint
            ? result
            : IntegrationMessageProcessingResult.DeadLetter("ComboAnalysisReadyV1.IdentityPayloadConflict",
                "MessageId has already been used with another payload or Tenant.");
    }
}
