namespace AgriDrone.Api.Contracts.Missions;

public sealed record PrepareMissionSetRequest(
    Guid DroneId,
    Guid OperationId,
    DateTimeOffset? ServiceStartAt = null,
    DateTimeOffset? ServiceEndAt = null,
    DateTimeOffset? BaselineStartAt = null,
    DateTimeOffset? BaselineEndAt = null);
