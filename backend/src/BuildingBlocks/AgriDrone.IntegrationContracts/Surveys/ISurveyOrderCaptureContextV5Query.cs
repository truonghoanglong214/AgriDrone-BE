namespace AgriDrone.IntegrationContracts.Surveys;

public interface ISurveyOrderCaptureContextV5Query
{
    Task<SurveyOrderCaptureContextV5?> GetAsync(
        Guid surveyOrderId,
        CancellationToken cancellationToken = default);
}
