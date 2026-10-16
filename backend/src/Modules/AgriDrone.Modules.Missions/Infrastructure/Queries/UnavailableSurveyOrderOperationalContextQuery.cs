using AgriDrone.IntegrationContracts.Surveys;

namespace AgriDrone.Modules.Missions.Infrastructure.Queries;

internal sealed class SurveyOrderOperationalContextUnavailableException
    : Exception
{
}

internal sealed class UnavailableSurveyOrderOperationalContextQuery
    : ISurveyOrderOperationalContextQuery, ISurveyOrderOperationalContextV3Query
{
    public Task<SurveyOrderOperationalContextV2?> GetAsync(
        Guid surveyOrderId,
        CancellationToken cancellationToken = default) =>
        Task.FromException<SurveyOrderOperationalContextV2?>(
            new SurveyOrderOperationalContextUnavailableException());

    Task<SurveyOrderOperationalContextV3?> ISurveyOrderOperationalContextV3Query.GetAsync(
        Guid surveyOrderId, string requestedMissionPurpose,
        CancellationToken cancellationToken) =>
        Task.FromException<SurveyOrderOperationalContextV3?>(
            new SurveyOrderOperationalContextUnavailableException());
}
