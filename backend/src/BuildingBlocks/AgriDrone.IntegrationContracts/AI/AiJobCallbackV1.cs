namespace AgriDrone.IntegrationContracts.AI;

public sealed record AiJobCallbackV1(
    Guid JobId,
    Guid TenantId,
    Guid CorrelationId,
    string ExternalJobId,
    string Status,
    int AttemptNumber,
    long SequenceNumber,
    decimal ProgressPercent,
    IReadOnlyList<AiJobOutputV1> Outputs,
    string? ErrorCode,
    string? ErrorMessage,
    bool Retryable,
    DateTimeOffset OccurredAt);