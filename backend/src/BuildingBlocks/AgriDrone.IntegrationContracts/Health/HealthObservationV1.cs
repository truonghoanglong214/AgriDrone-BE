namespace AgriDrone.IntegrationContracts.Health;

public sealed record HealthObservationV1(
    Guid ObservationId,
    int ObservationVersion,
    Guid PlantId,
    Guid MediaAssetId,
    string EvidenceStorageUri,
    DateTimeOffset ObservedAt,
    string ConditionCode,
    string HealthLevelCode,
    decimal SeverityPercent,
    decimal Confidence);