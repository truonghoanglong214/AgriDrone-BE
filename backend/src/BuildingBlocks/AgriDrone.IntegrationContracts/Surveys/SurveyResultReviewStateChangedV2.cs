namespace AgriDrone.IntegrationContracts.Surveys;

public sealed record SurveyResultReviewStateChangedV2(
    Guid CausationId,
    Guid HandoffId,
    Guid SurveyOrderId,
    Guid MissionId,
    Guid FarmId,
    Guid SurveyResultId,
    string ServiceType,
    long ReviewVersion,
    string State,
    int TotalItems,
    int PendingItems,
    int CorrectedItems,
    DateTimeOffset ChangedAt);
