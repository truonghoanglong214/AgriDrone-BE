namespace AgriDrone.IntegrationContracts.Health;

public sealed record HealthObservationsReadyV1(
    Guid HandoffId,
    Guid MissionId,
    Guid FarmId,
    Guid ZoneId,
    Guid JobId,
    Guid ModelVersionId,
    string ModelVersion,
    Guid? ThresholdProfileId,
    string? ThresholdProfileVersion,
    IReadOnlyList<HealthObservationV1> Observations);