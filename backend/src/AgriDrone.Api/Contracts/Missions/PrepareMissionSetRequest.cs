namespace AgriDrone.Api.Contracts.Missions;

public sealed record PrepareMissionSetRequest(
    Guid DroneId,
    Guid OperationId,
    DateTimeOffset ServiceStartAt,
    DateTimeOffset ServiceEndAt,
    DateTimeOffset? BaselineStartAt,
    DateTimeOffset? BaselineEndAt);
