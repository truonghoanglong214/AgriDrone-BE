using System.Text.Json;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class PaymentEvent : Entity
{
    private PaymentEvent() { }

    public Guid SurveyPaymentId { get; private set; }
    public string Provider { get; private set; } = null!;
    public string ProviderReference { get; private set; } = null!;
    public string ProviderEventId { get; private set; } = null!;
    public string DeduplicationKey { get; private set; } = null!;
    public string EventType { get; private set; } = null!;
    public JsonDocument Payload { get; private set; } = null!;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public SurveyPayment SurveyPayment { get; private set; } = null!;

    public static PaymentEvent Create(
        Guid surveyPaymentId,
        PaymentEventIdentity identity,
        string eventType,
        JsonDocument payload,
        DateTimeOffset occurredAt,
        DateTimeOffset receivedAt)
    {
        DomainGuard.NotEmpty(surveyPaymentId);
        DomainGuard.Utc(occurredAt);
        DomainGuard.Utc(receivedAt);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentNullException.ThrowIfNull(payload);

        var normalizedEventType = eventType.Trim().ToLowerInvariant();
        if (normalizedEventType.Length > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(eventType),
                "Payment event type cannot exceed 100 characters.");
        }

        return new PaymentEvent
        {
            Id = Guid.CreateVersion7(),
            SurveyPaymentId = surveyPaymentId,
            Provider = identity.Provider,
            ProviderReference = identity.ProviderReference,
            ProviderEventId = identity.ProviderEventId,
            DeduplicationKey = identity.DeduplicationKey,
            EventType = normalizedEventType,
            Payload = JsonDocument.Parse(payload.RootElement.GetRawText()),
            OccurredAt = occurredAt,
            ReceivedAt = receivedAt
        };
    }
}
