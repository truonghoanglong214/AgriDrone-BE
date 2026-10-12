using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Errors;

public static class PriceAdjustmentError
{
    public const string InvalidTransitionCode =
        PriceAdjustmentDomainErrorCodes.InvalidTransition;
    public const string PendingAlreadyExistsCode =
        "PriceAdjustment.PendingAlreadyExists";
    public const string NotFoundCode = "PriceAdjustment.NotFound";
    public const string ForbiddenCode = "PriceAdjustment.Forbidden";
    public const string OrderNotEligibleCode = "PriceAdjustment.OrderNotEligible";
    public const string EffectivePriceUnavailableCode =
        "PriceAdjustment.EffectivePriceUnavailable";
    public const string VersionConflictCode =
        PriceAdjustmentDomainErrorCodes.VersionConflict;
    public const string NoChangeCode = PriceAdjustmentDomainErrorCodes.NoChange;
    public const string PersistenceConflictCode =
        "PriceAdjustment.PersistenceConflict";

    public static AppError NotFound(Guid adjustmentId) => AppError.NotFound(
        NotFoundCode,
        $"Price adjustment '{adjustmentId}' was not found.");

    public static AppError Forbidden() => AppError.Forbidden(
        ForbiddenCode,
        "The actor cannot request a price adjustment for this SurveyOrder.");

    public static AppError PendingAlreadyExists() => AppError.Conflict(
        PendingAlreadyExistsCode,
        "The SurveyOrder already has a pending or approved price adjustment.");

    public static AppError OrderNotEligible() => AppError.Conflict(
        OrderNotEligibleCode,
        "The SurveyOrder does not have an adjustable per-pole price snapshot.");

    public static AppError EffectivePriceUnavailable() => AppError.Conflict(
        EffectivePriceUnavailableCode,
        "Exactly one effective VND per-pole price is required for the adjustment.");

    public static AppError VersionConflict() => AppError.Conflict(
        VersionConflictCode,
        "The price adjustment or SurveyOrder changed concurrently.");

    public static AppError NoChange() => AppError.Conflict(
        NoChangeCode,
        "The proposed adjustment does not change the pole count or price snapshot.");

    public static AppError InvalidTransition(string description) =>
        AppError.Conflict(InvalidTransitionCode, description);

    public static AppError PersistenceConflict() => AppError.Conflict(
        PersistenceConflictCode,
        "Price-adjustment processing conflicted with another operation.");
}
