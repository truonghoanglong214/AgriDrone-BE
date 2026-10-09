namespace AgriDrone.IntegrationContracts.Surveys;

public interface ISurveyOrderOperationalContextV3Query
{
    Task<SurveyOrderOperationalContextV3?> GetAsync(
        Guid surveyOrderId,
        string requestedMissionPurpose,
        CancellationToken cancellationToken = default);
}
