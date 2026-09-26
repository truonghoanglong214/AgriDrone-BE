using AgriDrone.Modules.Surveys.Domain;

namespace AgriDrone.Modules.Surveys.Application.Errors;

public static class SurveyResultError
{
    public const string NotFoundCode = "SurveyResult.NotFound";
    public const string InvalidTransitionCode = SurveyResultDomainErrorCodes.InvalidTransition;
    public const string VersionConflictCode = SurveyResultDomainErrorCodes.VersionConflict;
    public const string DuplicateImportCode = "SurveyResult.DuplicateImport";
    public const string ForbiddenCode = "SurveyResult.Forbidden";
}
