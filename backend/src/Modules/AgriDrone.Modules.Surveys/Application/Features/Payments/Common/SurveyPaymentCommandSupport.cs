using AgriDrone.IntegrationContracts.Surveys;
using AgriDrone.Modules.Surveys.Application.Errors;
using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Features.Payments.Common;

internal static class SurveyPaymentCommandSupport
{
    public const string ProcessingEvent = "payment.processing";
    public const string SucceededEvent = "payment.succeeded";
    public const string FailedEvent = "payment.failed";
    public const string RefundedEvent = "payment.refunded";
    public const string ChargebackEvent = "payment.chargeback";

    public static bool IsSupportedEvent(string? eventType) =>
        !string.IsNullOrWhiteSpace(eventType) &&
        eventType.Trim().ToLowerInvariant() is
            ProcessingEvent or SucceededEvent or FailedEvent or
            RefundedEvent or ChargebackEvent;

    public static AppError MapDomainError(SurveyDomainException exception) =>
        exception.Code switch
        {
            PaymentDomainErrorCodes.VersionConflict =>
                PaymentError.VersionConflict(),
            PaymentDomainErrorCodes.InvalidTransition =>
                PaymentError.InvalidTransition(exception.Message),
            SurveyOrderDomainErrorCodes.VersionConflict =>
                SurveyOrderError.VersionConflict(),
            SurveyOrderDomainErrorCodes.InvalidTransition =>
                SurveyOrderError.InvalidTransition(exception.Message),
            _ => AppError.Conflict(exception.Code, exception.Message)
        };

    public static string MapStatus(SurveyPaymentStatus status) => status switch
    {
        SurveyPaymentStatus.Pending => "PENDING",
        SurveyPaymentStatus.Processing => "PROCESSING",
        SurveyPaymentStatus.Paid => "PAID",
        SurveyPaymentStatus.Failed => "FAILED",
        SurveyPaymentStatus.Refunded => "REFUNDED",
        SurveyPaymentStatus.AdjustmentRequired => "ADJUSTMENT_REQUIRED",
        _ => throw new ArgumentOutOfRangeException(nameof(status))
    };

    public static string MapOrderStatus(SurveyOrderStatus status) =>
        status switch
        {
            SurveyOrderStatus.PendingBoundaryVerification =>
                "PENDING_BOUNDARY_VERIFICATION",
            SurveyOrderStatus.AwaitingBaselineAppointment =>
                "AWAITING_BASELINE_APPOINTMENT",
            SurveyOrderStatus.AwaitingBoundaryCapture =>
                "AWAITING_BOUNDARY_CAPTURE",
            SurveyOrderStatus.BaselineReady => "BASELINE_READY",
            SurveyOrderStatus.BaselineInProgress => "BASELINE_IN_PROGRESS",
            SurveyOrderStatus.AwaitingBaselineReview =>
                "AWAITING_BASELINE_REVIEW",
            SurveyOrderStatus.AwaitingPricing => "AWAITING_PRICING",
            SurveyOrderStatus.AwaitingPaidAppointment =>
                "AWAITING_PAID_APPOINTMENT",
            SurveyOrderStatus.AwaitingPayment => "AWAITING_PAYMENT",
            SurveyOrderStatus.ReadyForPaidService =>
                "READY_FOR_PAID_SERVICE",
            SurveyOrderStatus.InProgress => "IN_PROGRESS",
            SurveyOrderStatus.PendingReview => "PENDING_REVIEW",
            SurveyOrderStatus.Completed => "COMPLETED",
            SurveyOrderStatus.Cancelled => "CANCELLED",
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };

    public static SurveyPaymentChangedV1 CreateEvent(
        SurveyOrder order,
        SurveyPayment payment,
        string changeType,
        string disposition,
        DateTimeOffset changedAt) => new(
            payment.Id,
            order.Id,
            order.TenantId,
            order.FarmId,
            payment.Provider,
            payment.ProviderReference!,
            payment.Amount,
            payment.Currency,
            MapStatus(payment.Status),
            changeType,
            disposition,
            changedAt);
}
