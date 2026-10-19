namespace AgriDrone.IntegrationContracts.Surveys;

public interface ISurveyOrderOperationalContextV4Query
{
    Task<SurveyOrderOperationalContextV4?> GetAsync(
        Guid surveyOrderId,
        string requestedMissionPurpose,
        CancellationToken cancellationToken = default);
}
