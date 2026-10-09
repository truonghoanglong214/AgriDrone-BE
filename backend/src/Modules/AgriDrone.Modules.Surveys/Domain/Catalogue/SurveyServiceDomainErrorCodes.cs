namespace AgriDrone.Modules.Surveys.Domain;

public static class SurveyServiceDomainErrorCodes
{
    public const string InvalidLifecycleTransition =
        "SURVEY_SERVICE_INVALID_LIFECYCLE_TRANSITION";
    public const string RetiredServiceImmutable =
        "SURVEY_SERVICE_RETIRED_IMMUTABLE";
    public const string VersionConflict =
        "SURVEY_SERVICE_VERSION_CONFLICT";
}
