using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Errors;

public static class SurveyPricingError
{
    public static AppError Forbidden() => AppError.Forbidden(
        "SurveyPricing.Forbidden",
        "The actor cannot confirm pricing for this SurveyOrder.");

    public static AppError CurrentBaseMapNotFound() => AppError.Conflict(
        "SurveyPricing.CurrentBaseMapNotFound",
        "The Farm has no current published base map.");

    public static AppError ApprovedScopeNotFound() => AppError.Conflict(
        "SurveyPricing.ApprovedScopeNotFound",
        "The Order no longer references an approved Farm boundary and scope.");

    public static AppError BaseMapVersionConflict() => AppError.Conflict(
        "SurveyPricing.BaseMapVersionConflict",
        "The current published Farm base-map version changed before confirmation.");

    public static AppError InventoryEmpty() => AppError.Conflict(
        "SurveyPricing.InventoryEmpty",
        "The current published map has no authoritative active poles in the approved scope.");

    public static AppError InventoryOutsideApprovedScope() => AppError.Conflict(
        "SurveyPricing.InventoryOutsideApprovedScope",
        "The current map contains active poles outside the approved boundary or active Zone scope.");

    public static AppError EffectivePriceNotFound() => AppError.Conflict(
        "SurveyPricing.EffectivePriceNotFound",
        "No effective VND per-pole price exists for the selected Survey Service.");

    public static AppError EffectivePriceAmbiguous() => AppError.Conflict(
        "SurveyPricing.EffectivePriceAmbiguous",
        "More than one effective per-pole price exists for the selected Survey Service.");

    public static AppError BaselineSourceMismatch() => AppError.Conflict(
        "SurveyPricing.BaselineSourceMismatch",
        "The current base-map source does not match the Order baseline requirement.");

    public static AppError PersistenceConflict() => AppError.Conflict(
        "SurveyPricing.PersistenceConflict",
        "Pole-count and pricing confirmation conflicted with another operation.");
}
