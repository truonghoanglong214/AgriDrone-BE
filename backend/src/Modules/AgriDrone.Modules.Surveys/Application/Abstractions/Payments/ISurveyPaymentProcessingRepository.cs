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

    Task<Result<SurveyPriceAdjustmentRequestState>>
        GetForPriceAdjustmentRequestAsync(
            Guid surveyOrderId,
            Guid actorUserId,
            DateTimeOffset evaluatedAt,
            CancellationToken cancellationToken = default);

    Task<Result<SurveyPriceAdjustmentReviewState>>
        GetForPriceAdjustmentReviewAsync(
            Guid surveyOrderId,
            Guid adjustmentId,
            CancellationToken cancellationToken = default);

    Task<Result<SurveyPaymentReconciliationState>>
        GetForReconciliationAsync(
            Guid paymentId,
            CancellationToken cancellationToken = default);

    void AddPayment(SurveyPayment payment);
    void AddEvent(PaymentEvent paymentEvent);
    void AddPriceAdjustment(PriceAdjustment adjustment);
}

public sealed record SurveyPaymentOwnerState(
    SurveyOrder Order,
    Guid PaidServiceAppointmentId,
    SurveyPayment? ActivePayment);

public sealed record SurveyPaymentEventState(
    SurveyOrder Order,
    SurveyPayment Payment,
    bool EventAlreadyProcessed);

public sealed record EffectiveSurveyPrice(
    Guid SurveyServicePriceId,
    decimal PricePerPole,
    string Currency);

public sealed record SurveyPriceAdjustmentRequestState(
    SurveyOrder Order,
    EffectiveSurveyPrice EffectivePrice,
    PriceAdjustment? OpenAdjustment,
    SurveyPayment? ActivePayment);

public sealed record SurveyPriceAdjustmentReviewState(
    SurveyOrder Order,
    PriceAdjustment Adjustment,
    SurveyPayment? ActivePayment);

public sealed record SurveyPaymentReconciliationState(
    SurveyOrder Order,
    SurveyPayment Payment);
