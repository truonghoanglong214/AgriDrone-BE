namespace AgriDrone.IntegrationContracts.Health;

public sealed record HealthObservationsReadyV2(
    Guid CausationId,
    Guid HandoffId,
    Guid SurveyOrderId,
    Guid MissionId,
    Guid FarmId,
    Guid FarmBaseMapVersionId,
    Guid JobId,
    Guid ModelVersionId,
    string ModelVersion,
    Guid? ThresholdProfileId,
    string? ThresholdProfileVersion,
    IReadOnlyList<HealthObservationV2> Observations);

public sealed record HealthObservationV2(
    Guid ObservationId,
    int ObservationVersion,
    Guid ZoneId,
    Guid PlantId,
    Guid MediaAssetId,
    string EvidenceStorageUri,
    DateTimeOffset ObservedAt,
    string ConditionCode,
    string ConditionDefinitionVersion,
    string HealthLevelCode,
    decimal SeverityPercent,
    decimal Confidence);
