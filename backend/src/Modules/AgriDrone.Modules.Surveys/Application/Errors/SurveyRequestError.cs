using AgriDrone.Modules.Surveys.Domain;
using AgriDrone.SharedKernel.Application;

namespace AgriDrone.Modules.Surveys.Application.Errors;

public static class SurveyRequestError
{
    public const string NotFoundCode = "SurveyRequest.NotFound";
    public const string InvalidTransitionCode = SurveyRequestDomainErrorCodes.InvalidTransition;
    public const string VersionConflictCode = SurveyRequestDomainErrorCodes.VersionConflict;
    public const string DuplicateCode = "SurveyRequest.Duplicate";
    public const string InvalidInputCode = "SurveyRequest.InvalidInput";

    public static AppError NotFound(Guid id) =>
        AppError.NotFound(NotFoundCode, $"Survey request '{id}' was not found.");

    public static AppError InvalidTransition(string description) =>
        AppError.Conflict(InvalidTransitionCode, description);

    public static AppError VersionConflict() =>
        AppError.Conflict(
            VersionConflictCode,
            "The survey request was changed by another operation.");

    public static AppError InvalidInput(string description) =>
        AppError.Validation(InvalidInputCode, description);
}
