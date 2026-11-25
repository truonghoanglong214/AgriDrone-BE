using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Payments;

public interface ISurveyPaymentProcessingOutbox
{
    // Specialized persistence stages the existing publication notification atomically with settlement.
    Task AddPostFlightUnlockAsync(SurveyOrder order, Guid correlationId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    void AddReleaseEvaluationRequested(
        SurveyReleaseEvaluationRequestedV1 trigger,
        Guid correlationId,
        Guid? actorId,
        DateTimeOffset occurredAt)
    {
        var envelope = IntegrationEventEnvelopeFactory.Create(
            IntegrationEventDescriptors.SurveyReleaseEvaluationRequestedV1,
            Guid.CreateVersion7(),
            correlationId,
            trigger.TenantId,
            actorId,
            occurredAt,
            trigger);
        Add(envelope, trigger.OrderId.ToString("D"));
    }

    void Add<TPayload>(
        IntegrationEventEnvelope<TPayload> envelope,
        string? partitionKey = null);
}
