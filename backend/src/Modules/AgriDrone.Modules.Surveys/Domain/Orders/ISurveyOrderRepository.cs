namespace AgriDrone.Modules.Surveys.Domain;

/// <summary>
/// Approval-facing SurveyOrder persistence. Step 3B binds this repository to the
/// specialized approval DbContext so request mutation and order creation share a
/// transaction.
/// </summary>
public interface ISurveyOrderRepository
{
    Task<SurveyOrder?> GetBySurveyRequestIdAsync(
        Guid surveyRequestId,
        CancellationToken cancellationToken = default);

    void Add(SurveyOrder surveyOrder);
}
