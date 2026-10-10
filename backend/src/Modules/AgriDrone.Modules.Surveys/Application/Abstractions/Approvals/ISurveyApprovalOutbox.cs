using AgriDrone.IntegrationContracts.Messaging;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Approvals;

/// <summary>
/// Stages approval events in the same specialized DbContext as the business data.
/// </summary>
public interface ISurveyApprovalOutbox
{
    void Add<TPayload>(
        IntegrationEventEnvelope<TPayload> envelope,
        string? partitionKey = null);
}
