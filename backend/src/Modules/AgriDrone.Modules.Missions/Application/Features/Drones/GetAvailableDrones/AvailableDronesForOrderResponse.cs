namespace AgriDrone.Modules.Missions.Application.Features.Drones.GetAvailableDrones;

public sealed record AvailableDronesForOrderResponse(
    Guid SurveyOrderId,
    Guid FarmId,
    string Purpose,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    IReadOnlyList<AvailableDroneResponse> Drones);
