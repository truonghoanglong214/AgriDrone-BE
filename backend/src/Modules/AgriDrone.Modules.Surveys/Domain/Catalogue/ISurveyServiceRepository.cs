namespace AgriDrone.Modules.Surveys.Domain;

internal interface ISurveyServiceRepository
{
    Task<SurveyService?> GetByIdAsync(
        Guid surveyServiceId,
        CancellationToken cancellationToken = default);

    Task<SurveyService?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default);

    void Add(SurveyService surveyService);
}
