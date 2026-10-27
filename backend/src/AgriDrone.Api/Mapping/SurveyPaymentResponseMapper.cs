using AgriDrone.Api.Contracts.Surveys.Payments;
using AgriDrone.Modules.Surveys.Application.Features.Payments.InitiateSurveyPayment;
using AgriDrone.Modules.Surveys.Application.Features.Payments.RequestPriceAdjustment;
using AgriDrone.Modules.Surveys.Application.Features.Payments.ApprovePriceAdjustment;
using AgriDrone.Modules.Surveys.Application.Features.Payments.RejectPriceAdjustment;
using AgriDrone.Modules.Surveys.Application.Features.Payments.ApplyPriceAdjustment;
using AgriDrone.Modules.Surveys.Application.Features.Payments.ReconcileSurveyPayment;
using AgriDrone.Modules.Surveys.Application.Features.Payments.GetSurveyPaymentHistory;

namespace AgriDrone.Api.Mapping;

internal static class SurveyPaymentResponseMapper
{
    public static SurveyPaymentHistoryApiResponse ToResponse(
        SurveyPaymentHistoryResponse response) => new(
            response.SurveyOrderId,
            response.Payments.Select(payment => new SurveyPaymentHistoryItemApiResponse(
                payment.SurveyPaymentId,
                payment.Purpose,
                payment.Amount,
                payment.Currency,
                payment.Provider,
                payment.ProviderReference,
                payment.Status,
                payment.CreatedAt,
                payment.UpdatedAt,
                payment.ConfirmedAt,
                payment.PaymentVersion,
                payment.Events.Select(paymentEvent =>
                    new SurveyPaymentHistoryEventApiResponse(
                        paymentEvent.EventType,
                        paymentEvent.OccurredAt,
                        paymentEvent.ReceivedAt)).ToArray()))
                .ToArray());

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
            response.InitiatedAt,
            response.AppointmentId ?? response.PaidServiceAppointmentId);

    public static RequestedPriceAdjustmentApiResponse ToResponse(
        RequestPriceAdjustmentResponse response) => new(
            response.PriceAdjustmentId,
            response.SurveyOrderId,
            response.Status,
            response.OldPoleCount,
            response.NewPoleCount,
            response.OldSurveyServicePriceId,
            response.NewSurveyServicePriceId,
            response.OldPricePerPole,
            response.NewPricePerPole,
            response.OldFinalPrice,
            response.NewFinalPrice,
            response.Currency,
            response.Reason,
            response.EvidenceReference,
            response.RequestedBy,
            response.AdjustmentVersion,
            response.OrderVersion,
            response.RequestedAt);

    public static ReviewedPriceAdjustmentApiResponse ToResponse(
        ApprovePriceAdjustmentResponse response) => new(
            response.PriceAdjustmentId,
            response.SurveyOrderId,
            response.Status,
            response.ReviewedBy,
            response.ReviewReason,
            response.ReviewEvidenceReference,
            response.AdjustmentVersion,
            response.OrderVersion,
            response.ReviewedAt);

    public static ReviewedPriceAdjustmentApiResponse ToResponse(
        RejectPriceAdjustmentResponse response) => new(
            response.PriceAdjustmentId,
            response.SurveyOrderId,
            response.Status,
            response.ReviewedBy,
            response.ReviewReason,
            response.ReviewEvidenceReference,
            response.AdjustmentVersion,
            response.OrderVersion,
            response.ReviewedAt);

    public static AppliedPriceAdjustmentApiResponse ToResponse(
        ApplyPriceAdjustmentResponse response) => new(
            response.PriceAdjustmentId,
            response.SurveyOrderId,
            response.Status,
            response.ConfirmedPoleCount,
            response.SurveyServicePriceId,
            response.PricePerPole,
            response.FinalPrice,
            response.Currency,
            response.OrderStatus,
            response.PaymentStatus,
            response.AdjustmentVersion,
            response.OrderVersion,
            response.PaymentVersion,
            response.AppliedBy,
            response.AppliedAt);

    public static ReconcileSurveyPaymentApiResponse ToResponse(
        ReconcileSurveyPaymentResponse response) => new(
            response.SurveyPaymentId,
            response.SurveyOrderId,
            response.Provider,
            response.ProviderReference,
            response.Amount,
            response.Currency,
            response.PaymentStatus,
            response.OrderStatus,
            response.PaymentVersion,
            response.OrderVersion,
            response.ReconciledBy,
            response.ReconciledAt);
}
