namespace AgriDrone.Api.Contracts.Missions;

public sealed record PrepareMissionSetApiResponse(
    Guid SurveyOrderId,
    Guid FarmId,
    bool ReusedExistingSet,
    IReadOnlyList<PreparedMissionApiResponse> Missions);
