using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Errors;

public static class SurveyOrderError
{
    public const string NotFoundCode = "SurveyOrder.NotFound";
    public const string InvalidTransitionCode = SurveyOrderDomainErrorCodes.InvalidTransition;
    public const string VersionConflictCode = SurveyOrderDomainErrorCodes.VersionConflict;
    public const string NotReadyCode = "SurveyOrder.NotReady";
    public const string ForbiddenCode = "SurveyOrder.Forbidden";

    public static AppError NotFound(Guid id) =>
        AppError.NotFound(NotFoundCode, $"Survey order '{id}' was not found.");

    public static AppError InvalidTransition(string description) =>
        AppError.Conflict(InvalidTransitionCode, description);

    public static AppError VersionConflict() =>
        AppError.Conflict(
            VersionConflictCode,
            "The survey order was changed by another operation.");

    public static AppError NotReady(string description) =>
        AppError.Validation(NotReadyCode, description);

    public static AppError Forbidden() =>
        AppError.Forbidden(
            ForbiddenCode,
            "The actor cannot perform this survey-order action.");
}
