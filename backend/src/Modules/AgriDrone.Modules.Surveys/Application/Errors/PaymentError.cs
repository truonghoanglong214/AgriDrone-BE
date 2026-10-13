using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Errors;

public static class PaymentError
{
    public const string NotFoundCode = "Payment.NotFound";
    public const string InvalidTransitionCode = PaymentDomainErrorCodes.InvalidTransition;
    public const string VersionConflictCode = PaymentDomainErrorCodes.VersionConflict;
    public const string DuplicateEventCode = "Payment.DuplicateEvent";
    public const string InvalidEvidenceCode = "Payment.InvalidEvidence";
    public const string ForbiddenCode = "Payment.Forbidden";
    public const string OrderNotReadyCode = "Payment.OrderNotReady";
    public const string ActivePaymentExistsCode = "Payment.ActivePaymentExists";
    public const string ProviderUnavailableCode = "Payment.ProviderUnavailable";
    public const string ProviderMismatchCode = "Payment.ProviderMismatch";
    public const string UnsupportedEventCode = "Payment.UnsupportedEvent";
    public const string PersistenceConflictCode = "Payment.PersistenceConflict";
    public const string ReconciliationMismatchCode =
        "Payment.ReconciliationMismatch";
    public const string ReconciliationEvidenceRequiredCode =
        "Payment.ReconciliationEvidenceRequired";

    public static AppError NotFound(string providerReference) =>
        AppError.NotFound(
            NotFoundCode,
            $"Payment provider reference '{providerReference}' was not found.");

    public static AppError Forbidden() => AppError.Forbidden(
        ForbiddenCode,
        "The actor cannot initiate payment for this SurveyOrder.");

    public static AppError OrderNotReady() => AppError.Conflict(
        OrderNotReadyCode,
        "A confirmed current paid-service appointment and immutable price snapshot are required before payment initiation.");

    public static AppError ActivePaymentExists() => AppError.Conflict(
        ActivePaymentExistsCode,
        "An active payment already exists for this SurveyOrder.");

    public static AppError ProviderUnavailable() => AppError.Failure(
        ProviderUnavailableCode,
        "The configured survey-payment provider is unavailable.");

    public static AppError ProviderMismatch() => AppError.Conflict(
        ProviderMismatchCode,
        "The callback provider does not match the payment provider.");

    public static AppError UnsupportedEvent(string eventType) =>
        AppError.Validation(
            UnsupportedEventCode,
            $"Payment event type '{eventType}' is not supported.");

    public static AppError VersionConflict() => AppError.Conflict(
        VersionConflictCode,
        "The payment or SurveyOrder was changed by another operation.");

    public static AppError InvalidTransition(string description) =>
        AppError.Conflict(InvalidTransitionCode, description);

    public static AppError PersistenceConflict() => AppError.Conflict(
        PersistenceConflictCode,
        "Payment processing conflicted with another operation.");

    public static AppError ReconciliationMismatch() => AppError.Conflict(
        ReconciliationMismatchCode,
        "Reconciled provider amount/currency must match the current SurveyOrder snapshot.");
}
