using AgriDrone.IntegrationContracts.Messaging;
using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Payments;

public interface ISurveyPaymentProcessingOutbox
{
    // Specialized persistence stages the existing publication notification atomically with settlement.
    Task AddPostFlightUnlockAsync(SurveyOrder order, Guid correlationId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    void Add<TPayload>(
        IntegrationEventEnvelope<TPayload> envelope,
        string? partitionKey = null);
}
