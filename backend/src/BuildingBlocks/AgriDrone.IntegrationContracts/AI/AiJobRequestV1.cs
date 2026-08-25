namespace AgriDrone.IntegrationContracts.AI;

public sealed record AiJobRequestV1(
    Guid JobId,
    Guid MissionId,
    Guid TenantId,
    Guid CorrelationId,
    string JobType,
    int AttemptNumber,
    Guid ModelVersionId,
    string ModelVersion,
    Guid? ThresholdProfileId,
    string? ThresholdProfileVersion,
    IReadOnlyDictionary<string, string> Parameters,
    IReadOnlyList<AiJobInputV1> Inputs,
    string CallbackUrl,
    DateTimeOffset RequestedAt);