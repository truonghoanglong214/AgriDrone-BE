using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Abstractions.Payments;

public interface ISurveyPaymentProcessingRepository
{
    Task<Result<SurveyPaymentOwnerState>> GetForOwnerInitiationAsync(
        Guid surveyOrderId,
        Guid actorUserId,
        DateTimeOffset evaluatedAt,
        CancellationToken cancellationToken = default);

    Task<Result<SurveyPaymentEventState>> GetForProviderEventAsync(
        string provider,
        string providerReference,
        string deduplicationKey,
        CancellationToken cancellationToken = default);

    void AddPayment(SurveyPayment payment);
    void AddEvent(PaymentEvent paymentEvent);
}

public sealed record SurveyPaymentOwnerState(
    SurveyOrder Order,
    Guid PaidServiceAppointmentId,
    SurveyPayment? ActivePayment);

public sealed record SurveyPaymentEventState(
    SurveyOrder Order,
    SurveyPayment Payment,
    bool EventAlreadyProcessed);
