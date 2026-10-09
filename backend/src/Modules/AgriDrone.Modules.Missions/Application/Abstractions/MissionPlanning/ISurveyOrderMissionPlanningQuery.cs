namespace AgriDrone.Modules.Missions.Application.Abstractions.MissionPlanning;

public interface ISurveyOrderMissionPlanningQuery
{
    Task<SurveyOrderMissionPlanningContext?> GetAsync(
        Guid surveyOrderId,
        CancellationToken cancellationToken = default);

    Task<SurveyOrderMissionPlanningContext?> GetForPurposeAsync(
        Guid surveyOrderId,
        AgriDrone.Modules.Missions.Domain.Missions.MissionPurpose purpose,
        CancellationToken cancellationToken = default) =>
        GetAsync(surveyOrderId, cancellationToken);
}
