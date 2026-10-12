using AgriDrone.Api.Contracts.Surveys.Payments;
using AgriDrone.Modules.Surveys.Application.Features.Payments.InitiateSurveyPayment;
using AgriDrone.Modules.Surveys.Application.Features.Payments.ProcessSurveyPaymentEvent;
using AgriDrone.Modules.Surveys.Application.Features.Payments.RequestPriceAdjustment;
using AgriDrone.Modules.Surveys.Application.Features.Payments.ApprovePriceAdjustment;
using AgriDrone.Modules.Surveys.Application.Features.Payments.RejectPriceAdjustment;
using AgriDrone.Modules.Surveys.Application.Features.Payments.ApplyPriceAdjustment;
using AgriDrone.Modules.Surveys.Application.Features.Payments.ReconcileSurveyPayment;

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
