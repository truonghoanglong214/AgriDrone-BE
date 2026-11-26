using AgriDrone.IntegrationContracts.Surveys;

namespace AgriDrone.Modules.Missions.Infrastructure.Queries;

internal sealed class UnavailableSurveyOrderCommercialSnapshotQuery : ISurveyOrderCommercialSnapshotQuery
{
    public Task<SurveyOrderMappingContext?> GetMappingContextAsync(
        Guid surveyOrderId,
        Guid tenantId,
        Guid farmId,
        Guid missionId,
        Guid captureAttemptId,
        CancellationToken cancellationToken) =>
        Task.FromResult<SurveyOrderMappingContext?>(null);

    public Task<PinnedSurveyOrderCommercialSnapshot?> GetByOrderIdAsync(
        Guid surveyOrderId, CancellationToken cancellationToken = default) =>
        Task.FromResult<PinnedSurveyOrderCommercialSnapshot?>(null);
}
