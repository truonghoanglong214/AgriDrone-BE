using AgriDrone.Api.Contracts.Surveys.Payments;
using AgriDrone.Modules.Surveys.Application.Features.Payments.InitiateSurveyPayment;
using AgriDrone.Modules.Surveys.Application.Features.Payments.ProcessSurveyPaymentEvent;

namespace AgriDrone.Api.Mapping;

internal static class SurveyPaymentResponseMapper
{
    public static InitiateSurveyPaymentApiResponse ToResponse(
        InitiateSurveyPaymentResponse response) => new(
            response.SurveyPaymentId,
            response.SurveyOrderId,
            response.PaidServiceAppointmentId,
            response.Amount,
            response.Currency,
            response.Provider,
            response.ProviderReference,
            response.PaymentStatus,
            response.RedirectUrl,
            response.ClientToken,
            response.ExpiresAt,
            response.PaymentVersion,
            response.OrderVersion,
            response.InitiatedAt);

    public static SurveyPaymentCallbackResponse ToResponse(
        ProcessSurveyPaymentEventResponse response) => new(
            response.SurveyPaymentId,
            response.SurveyOrderId,
            response.Provider,
            response.ProviderReference,
            response.ProviderEventId,
            response.EventType,
            response.PaymentStatus,
            response.OrderStatus,
            response.Duplicate,
            response.TransitionApplied,
            response.Disposition,
            response.ProcessedAt);
}
