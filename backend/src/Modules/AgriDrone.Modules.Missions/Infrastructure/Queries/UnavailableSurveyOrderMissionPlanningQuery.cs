using AgriDrone.Modules.Missions.Application.Abstractions.MissionPlanning;

namespace AgriDrone.Modules.Missions.Infrastructure.Queries;

internal sealed class UnavailableSurveyOrderMissionPlanningQuery
    : ISurveyOrderMissionPlanningQuery
{
    public Task<SurveyOrderMissionPlanningContext?> GetAsync(
        Guid surveyOrderId,
        CancellationToken cancellationToken = default) =>
        Task.FromException<SurveyOrderMissionPlanningContext?>(
            new SurveyOrderMissionPlanningUnavailableException());
}
