namespace AgriDrone.Modules.Surveys.Domain;

internal interface ISurveyRequestRepository
{
    Task<SurveyRequest?> GetByIdAsync(
        Guid surveyRequestId,
        CancellationToken cancellationToken = default);

    Task<SurveyRequest?> GetByCallerScopeAndIdempotencyKeyAsync(
        RequestIdempotency idempotency,
        CancellationToken cancellationToken = default);

    void Add(SurveyRequest surveyRequest);
}
