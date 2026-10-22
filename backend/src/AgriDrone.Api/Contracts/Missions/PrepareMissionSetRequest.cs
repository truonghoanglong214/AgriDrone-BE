namespace AgriDrone.Api.Contracts.Missions;

public sealed record PrepareMissionSetRequest(
    Guid DroneId,
    DateOnly? ServiceDate = null,
    DateOnly? BaselineDate = null);
