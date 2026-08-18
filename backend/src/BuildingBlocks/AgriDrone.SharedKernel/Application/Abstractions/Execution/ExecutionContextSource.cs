namespace AgriDrone.SharedKernel.Application.Abstractions.Execution;

public enum ExecutionContextSource
{
    Unknown = 0,
    Http = 1,
    RabbitMq = 2,
    System = 3
}
