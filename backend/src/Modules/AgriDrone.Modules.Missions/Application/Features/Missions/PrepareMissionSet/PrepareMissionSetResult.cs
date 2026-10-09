namespace AgriDrone.Modules.Missions.Application.Features.Missions.PrepareMissionSet;

public sealed record PrepareMissionSetResult(
    Guid SurveyOrderId,
    Guid FarmId,
    bool ReusedExistingSet,
    IReadOnlyList<PreparedMissionResult> Missions);
