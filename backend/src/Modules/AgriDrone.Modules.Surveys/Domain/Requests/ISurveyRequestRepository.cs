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

    Task<SurveyRequestQuoteSnapshot?> GetQuoteSnapshotByRequestIdAsync(
        Guid requestId,
        CancellationToken cancellationToken = default);

    Task<SurveyRequestTracking?> GetTrackingByRequestIdAsync(
        Guid requestId,
        CancellationToken cancellationToken = default);

    Task<SurveyRequestTracking?> GetTrackingByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    void AddQuoteSnapshot(SurveyRequestQuoteSnapshot snapshot);

    void AddTracking(SurveyRequestTracking tracking);
}
