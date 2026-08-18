namespace AgriDrone.SharedKernel.Application.Abstractions.Execution;

public interface IExecutionContext
{
    bool IsInitialized { get; }

    Guid? TenantId { get; }

    Guid? ActorId { get; }

    Guid CorrelationId { get; }

    Guid? MessageId { get; }

    ExecutionContextSource Source { get; }
}
