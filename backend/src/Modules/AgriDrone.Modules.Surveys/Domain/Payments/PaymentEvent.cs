using System.Text.Json;
using AgriDrone.SharedKernel.Domain;

namespace AgriDrone.Modules.Surveys.Domain;

public sealed class PaymentEvent : Entity
{
    private PaymentEvent() { }

    public Guid SurveyPaymentId { get; private set; }
    public string Provider { get; private set; } = null!;
    public string DeduplicationKey { get; private set; } = null!;
    public string EventType { get; private set; } = null!;
    public JsonDocument Payload { get; private set; } = null!;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset ReceivedAt { get; private set; }
    public SurveyPayment SurveyPayment { get; private set; } = null!;
}
