namespace AgriDrone.SharedKernel.Application.Abstractions.Execution;

public sealed record ExecutionContextSnapshot(
    Guid? TenantId,
    Guid? ActorId,
    Guid CorrelationId,
    Guid? MessageId,
    ExecutionContextSource Source);
