using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Errors;

public static class SurveyServiceError
{
    public static AppError NotFound() =>
        AppError.NotFound(
            "SurveyService.NotFound",
            "Survey service was not found.");

    public static AppError CurrentUserRequired() =>
        AppError.Unauthorized(
            "SurveyService.CurrentUserRequired",
            "An authenticated system administrator is required.");

    public static AppError ConcurrentUpdate() =>
        AppError.Conflict(
            "SurveyService.ConcurrentUpdate",
            "The survey service changed in another request. Reload it and try again.");

    public static AppError InvalidLifecycle(string description) =>
        AppError.Conflict(
            "SurveyService.InvalidLifecycle",
            description);

    public static AppError ActivePriceRequired() =>
        AppError.Conflict(
            "SurveyService.ActivePriceRequired",
            "An effective VND per-pole price is required before activation.");

    public static AppError PriceWindowOverlap() =>
        AppError.Conflict(
            "SurveyService.PriceWindowOverlap",
            "The proposed price window overlaps another per-pole price version.");

    public static AppError UnsupportedCurrency() =>
        AppError.Validation(
            "SurveyService.UnsupportedCurrency",
            "Only VND per-pole prices are supported.");

    public static AppError PriceEffectiveFromInPast() =>
        AppError.Validation(
            "SurveyService.PriceEffectiveFromInPast",
            "A new price version cannot take effect before server time.");
}
