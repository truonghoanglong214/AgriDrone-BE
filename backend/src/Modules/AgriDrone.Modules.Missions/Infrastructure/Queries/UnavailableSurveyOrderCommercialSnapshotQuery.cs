using AgriDrone.IntegrationContracts.Surveys;

namespace AgriDrone.Modules.Missions.Infrastructure.Queries;

internal sealed class UnavailableSurveyOrderCommercialSnapshotQuery : ISurveyOrderCommercialSnapshotQuery
{
    public Task<PinnedSurveyOrderCommercialSnapshot?> GetByOrderIdAsync(
        Guid surveyOrderId, CancellationToken cancellationToken = default) =>
        Task.FromResult<PinnedSurveyOrderCommercialSnapshot?>(null);
}
