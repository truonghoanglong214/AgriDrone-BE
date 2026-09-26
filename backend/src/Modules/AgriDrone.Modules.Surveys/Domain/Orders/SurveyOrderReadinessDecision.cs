namespace AgriDrone.Modules.Surveys.Domain;

public sealed record SurveyOrderReadinessDecision(
    bool IsReady,
    IReadOnlyCollection<SurveyOrderReadinessFailure> Failures);
