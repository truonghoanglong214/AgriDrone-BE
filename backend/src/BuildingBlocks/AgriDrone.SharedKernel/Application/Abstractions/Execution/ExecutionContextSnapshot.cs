namespace AgriDrone.SharedKernel.Application.Abstractions.Execution;

public sealed record ExecutionContextSnapshot(
    Guid? TenantId,
    Guid? ActorId,
    Guid CorrelationId,
    Guid? MessageId,
    ExecutionContextSource Source)
{
    public static ExecutionContextSnapshot ForHttp(
        Guid? tenantId,
        Guid? actorId,
        Guid correlationId)
    {
        return new ExecutionContextSnapshot(
            tenantId,
            actorId,
            correlationId,
            MessageId: null,
            ExecutionContextSource.Http);
    }

    public static ExecutionContextSnapshot ForRabbitMq(
        Guid tenantId,
        Guid? actorId,
        Guid correlationId,
        Guid messageId)
    {
        return new ExecutionContextSnapshot(
            tenantId,
            actorId,
            correlationId,
            messageId,
            ExecutionContextSource.RabbitMq);
    }
}
