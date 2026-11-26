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

public sealed record SurveyOrderMappingContext(
    Guid FarmBoundaryVersionId,
    bool RequiresBaselineMapping,
    Guid? FarmBaseMapVersionId);

public interface ISurveyOrderCommercialSnapshotQuery
{
    Task<SurveyOrderMappingContext?> GetMappingContextAsync(
        Guid surveyOrderId,
        Guid tenantId,
        Guid farmId,
        Guid missionId,
        Guid captureAttemptId,
        CancellationToken cancellationToken);

    Task<PinnedSurveyOrderCommercialSnapshot?> GetByOrderIdAsync(
        Guid surveyOrderId,
        CancellationToken cancellationToken = default);
}
