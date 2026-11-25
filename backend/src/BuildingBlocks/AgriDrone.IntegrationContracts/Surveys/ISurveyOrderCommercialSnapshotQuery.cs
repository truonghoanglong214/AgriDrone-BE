namespace AgriDrone.IntegrationContracts.Surveys;

public sealed record PinnedSurveyOrderCommercialSnapshot(
    Guid SurveyOrderId,
    Guid BusinessPolicyVersionId,
    string ServiceCode,
    bool MapsPoles,
    bool ScreensDisease,
    Guid PriceVersionId,
    decimal PricePerPole,
    string Currency);

public interface ISurveyOrderCommercialSnapshotQuery
{
    Task<PinnedSurveyOrderCommercialSnapshot?> GetByOrderIdAsync(
        Guid surveyOrderId,
        CancellationToken cancellationToken = default);
}
