namespace AgriDrone.Modules.Surveys.Domain;

public static class SurveyOrderDomainErrorCodes
{
    public const string InvalidTransition = "SurveyOrder.InvalidTransition";
    public const string VersionConflict = "SurveyOrder.VersionConflict";
    public const string BaselineMappingRequired = "SurveyOrder.BaselineMappingRequired";
    public const string BaselineMappingNotRequired = "SurveyOrder.BaselineMappingNotRequired";
    public const string PricingSnapshotIncomplete = "SurveyOrder.PricingSnapshotIncomplete";
}
